using Luizio.iFX.Client;
using Luizio.iFX.Models;
using Luizio.iFX.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Luizio.iFX.Messaging;

public class RabbitMqSubscriber(IServiceProvider serviceProvider, IRabbitMqConnectionFactory connectionFactory, ILogger<RabbitMqSubscriber> logger) : IHostedService
{
    private IRabbitMqConnection? connection;
    private List<IRabbitMqChannel> channels = [];
    private readonly List<IRabbitMqConsumer> consumers = [];
    private const string XRetryCount = "x-retry-count";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        await Subscribe(connection);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var channel in channels)
        {
            await channel.DisposeAsync();
        }
        consumers.Clear();
        if (connection != null)
        {
            await connection.CloseAsync(cancellationToken);
            await connection.DisposeAsync();
        }
    }

    public async Task Subscribe(IRabbitMqConnection connection)
    {
        var subscriptions = serviceProvider.GetRequiredService<SubscriptionStore>().GetSubscriptions();
        foreach (var subscription in subscriptions)
        {
            logger.LogInformation($"Subscribing to {subscription.Topic}_{subscription.Service}_{subscription.Method.Name.ToLower()}");
            var channel = await connection.CreateChannelAsync();
            channels.Add(channel);

            var queueName = $"{subscription.Topic}_{subscription.Service}_{subscription.Method.Name.ToLower()}";
            await channel.QueueDeclareAsync(queueName, true, false, false, null);
            await channel.QueueBindAsync(queueName, subscription.Topic, string.Empty);
            if (subscription.PrefetchCount > 0)
            {
                await channel.BasicQosAsync(0, subscription.PrefetchCount, false);
            }
            var consumer = channel.CreateConsumer();
            consumers.Add(consumer);

            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                if (subscription.Method is null)
                {
                    logger.LogError("Subscription method not set for event \"{Topic}\".", ea.Exchange);
                    return;
                }
                logger.LogInformation("Event received on {Subscription}.", subscription.Topic);
                var message = System.Text.Json.JsonSerializer.Deserialize(Encoding.UTF8.GetString(body), subscription.Method.GetParameters().First().ParameterType);
                if (message != null)
                {
                    var error = new Error();
                    try
                    {
                        using var scope = serviceProvider.CreateScope();
                        var currentUser = scope.ServiceProvider.GetRequiredService<CurrentUser>();
                        if (ea.BasicProperties.Headers != null)
                        {
                            var headers = ea.BasicProperties.Headers;

                            var metadata = headers
                                .SelectMany(header =>
                                {
                                    var key = header.Key;
                                    var valueList = header.Value switch
                                    {
                                        byte[] byteArray => JsonSerializer.Deserialize<List<string>>(Encoding.UTF8.GetString(byteArray)) ?? [],
                                        _ => []
                                    };

                                    return valueList.Select(value => new KeyValuePair<string, string>(key, value));
                                })
                                .ToList();
                            currentUser.Metadata = metadata;
                        }

                        var scopedProxy = scope.ServiceProvider.GetRequiredService<IProxy>();
                        var createMethod = typeof(IProxy).GetMethods().First(m => m.Name == "Create" && m.GetParameters().Length == 3);
                        var serviceProxy = createMethod.MakeGenericMethod(subscription.ServiceType!).Invoke(scopedProxy, [subscription.Service, subscription.Service, ProxyType.InProc]);

                        var task = (Task)subscription.Method!.Invoke(serviceProxy, [message])!;

                        var resultProperty = subscription.Method.ReturnType.GetProperty(nameof(Task<object>.Result));
                        var result = resultProperty!.GetValue(task);
                        if (result != null)
                        {
                            var errorProperty = result.GetType().GetProperty(nameof(Response<object>.Error));
                            error = errorProperty?.GetValue(result, null) as Error ?? new Error();
                        }
                    }
                    catch (Exception e)
                    {
                        error = new Error(ErrorCode.Exception, e.ToString());
                    }

                    if (!error.HasError)
                    {
                        await channel.BasicAckAsync(ea.DeliveryTag, false);
                        logger.LogInformation("Event successfully processed event on topic {Topic}.", ea.Exchange);
                    }
                    else
                    {
                        var shouldRequeue = error.Code == ErrorCode.Exception;
                        var retryCount = 0;

                        if (ea.BasicProperties.Headers?.TryGetValue(XRetryCount, out var xretryCount) == true)
                        {
                            retryCount = Convert.ToInt32(xretryCount);
                        }
                        retryCount++;
                        var newProperties = new BasicProperties
                        {
                            Headers = ea.BasicProperties.Headers ?? new Dictionary<string, object?>()
                        };
                        newProperties.Headers[XRetryCount] = retryCount;

                        shouldRequeue = shouldRequeue && retryCount <= subscription.RetryCount;

                        if (shouldRequeue)
                        {
                            await channel.BasicPublishAsync(ea.Exchange, ea.RoutingKey, true, newProperties, ea.Body);
                        }
                        await channel.BasicNackAsync(ea.DeliveryTag, false, false);
                        logger.LogError("Failed to process event on topic {Topic}. Retrying {Retrying}, retry count {RetryCount}. Error Code: {ErrorCode} message: {Error}", ea.Exchange, shouldRequeue, retryCount, error.Code, error.Description);
                    }
                }
                else
                {
                    logger.LogError("Empty event received for \"{Topic}\".", ea.Exchange);
                }
            };

            await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer);
        }
    }
}
