﻿using Luizio.ServiceProxy.Client;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Server;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Luizio.ServiceProxy.Messaging;
public class RabbitMqSubscriber : IHostedService
{
    private readonly IServiceProvider serviceProvider;
    private readonly IProxy proxy;
    private readonly ILogger<RabbitMqSubscriber> logger;
    private readonly IConnection connection;
    private const string XRetryCount = "x-retry-count";

    public RabbitMqSubscriber(IServiceProvider serviceProvider, IProxy proxy, RabbitMQ.Client.IConnectionFactory connectionFactory, ILogger<RabbitMqSubscriber> logger)
    {
        this.serviceProvider = serviceProvider;
        this.proxy = proxy;
        this.logger = logger;
        connection = connectionFactory.CreateConnection();
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        Subscribe(connection);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        connection.Close();
        return Task.CompletedTask;
    }

    public void Subscribe(IConnection connection)
    {
        var subscriptions = ServiceStore.GetSubscriptions();
        foreach (var subscription in subscriptions)
        {
            logger.LogInformation($"Subscribing to {subscription.Topic}_{subscription.Service}_{subscription.Method.Name.ToLower()}");
            var channel = connection.CreateModel();
            channel.ExchangeDeclare(exchange: subscription.Topic, durable: true, type: ExchangeType.Fanout);

            var queueName = $"{subscription.Topic}_{subscription.Service}_{subscription.Method.Name.ToLower()}";
            channel.QueueDeclare(queueName, true, false, false, null);
            channel.QueueBind(queueName, subscription.Topic, string.Empty);

            var consumer = new EventingBasicConsumer(channel);

            consumer.Received += (model, ea) =>
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
                            // Convert to List<KeyValuePair<string, string>> using LINQ
                            currentUser.Metadata = ea.BasicProperties.Headers
                                .Where(kvp => kvp.Value is List<string>) // Filter entries where value is List<string>
                                .SelectMany(kvp => ((List<string>)kvp.Value)
                                    .Select(value => new KeyValuePair<string, string>(kvp.Key, value)))
                                .ToList();
                        }

                        var service = ServiceStore.GetService(subscription.Service, scope.ServiceProvider);
                        var task = (Task)subscription.Method.Invoke(service, new object[] { message });

                        var resultProperty = subscription.Method.ReturnType.GetProperty(nameof(Task<object>.Result));
                        var result = resultProperty.GetValue(task);
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
                        channel.BasicAck(ea.DeliveryTag, false);
                        logger.LogInformation("Event successfully processed event on topic {Topic}.", ea.Exchange);
                    }
                    else
                    {

                        var shouldRequeue = error.Code == ErrorCode.Exception;

                        var retryCount = 0;
                        if (ea.BasicProperties.Headers == null)
                        {
                            ea.BasicProperties.Headers = new Dictionary<string, object>
                            {
                                {XRetryCount, retryCount }
                            };
                        }

                        if (ea.BasicProperties.Headers.TryGetValue(XRetryCount, out var xretryCount))
                        {
                            retryCount = (int)xretryCount;
                        }
                        retryCount++;
                        ea.BasicProperties.Headers[XRetryCount] = retryCount;

                        shouldRequeue = shouldRequeue && retryCount <= subscription.RetryCount;

                        if (shouldRequeue)
                        {
                            channel.BasicPublish(ea.Exchange, ea.RoutingKey, ea.BasicProperties, ea.Body);
                        }
                        channel.BasicNack(ea.DeliveryTag, false, false);
                        logger.LogError("Failed to process event on topic {Topic}. Retrying {Retrying}, retry count {RetryCount}", ea.Exchange, shouldRequeue, retryCount);
                    }
                }
                else
                {
                    logger.LogError("Empty event received for \"{Topic}\".", ea.Exchange);
                }

            };

            channel.BasicConsume(queue: queueName, autoAck: false, consumer: consumer);
        }
    }
}
