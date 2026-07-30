using Luizio.iFX.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Luizio.iFX.Messaging;

public class RabbitMqSubscriber(IServiceProvider serviceProvider, IRabbitMqConnectionFactory connectionFactory, ILogger<RabbitMqSubscriber> logger) : IHostedService
{
    private IRabbitMqConnection? connection;
    private readonly List<IRabbitMqChannel> channels = [];
    private readonly List<IRabbitMqConsumer> consumers = [];

    private readonly record struct Delivery(
        IRabbitMqChannel Channel,
        BasicDeliverEventArgs Args,
        Subscription Subscription,
        Activity? Activity,
        string? EventType = null)
    {
        internal string EventTypeTag => EventType ?? MessagingOutcome.UnknownEventType;
    }

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
            await SubscribeToQueue(connection, subscription);
    }

    private async Task SubscribeToQueue(IRabbitMqConnection connection, Subscription subscription)
    {
        var queueName = subscription.QueueName;
        logger.LogInformation("Subscribing to {QueueName}.", queueName);

        var channel = await connection.CreateChannelAsync();
        channels.Add(channel);

        IDictionary<string, object?>? queueArguments = null;
        if (subscription.HasDeadLetterQueue)
        {
            await channel.QueueDeclareAsync(subscription.DeadLetterQueue!, true, false, false, null);
            queueArguments = new Dictionary<string, object?>
            {
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = subscription.DeadLetterQueue
            };
        }

        await channel.QueueDeclareAsync(queueName, true, false, false, queueArguments);

        foreach (var exchange in subscription.BoundExchanges)
        {
            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true);
            await channel.QueueBindAsync(queueName, exchange, string.Empty);
        }

        if (subscription.PrefetchCount > 0)
            await channel.BasicQosAsync(0, subscription.PrefetchCount, false);

        var consumer = channel.CreateConsumer();
        consumers.Add(consumer);
        consumer.ReceivedAsync += (_, ea) => HandleMessageAsync(channel, ea, subscription);

        await channel.BasicConsumeAsync(queueName, autoAck: false, consumer: consumer);
    }

    private async Task HandleMessageAsync(IRabbitMqChannel channel, BasicDeliverEventArgs ea, Subscription subscription)
    {
        using var activity = StartConsumeActivity(ea, subscription);
        var delivery = new Delivery(channel, ea, subscription, activity);

        if (subscription.Invoker is null)
        {
            logger.LogError("Subscription invoker not set for queue \"{QueueName}\".", subscription.QueueName);
            await RejectAsync(delivery);
            return;
        }

        var eventType = ReadEventTypeHeader(ea.BasicProperties.Headers) ?? ea.Exchange;
        if (!subscription.TypesByExchange.TryGetValue(eventType, out var concreteType))
        {
            logger.LogError(
                "Event type \"{EventType}\" is not bound to queue \"{QueueName}\" and can never be processed. A stale binding left by a narrowed bindTo list is the likely cause.",
                eventType, subscription.QueueName);
            await RejectAsync(delivery);
            return;
        }

        delivery = delivery with { EventType = eventType };
        activity?.SetTag("messaging.message.type", eventType);

        object? message;
        try
        {
            message = JsonSerializer.Deserialize(Encoding.UTF8.GetString(ea.Body.ToArray()), concreteType);
        }
        catch (JsonException e)
        {
            logger.LogError(e, "Malformed \"{EventType}\" payload on queue \"{QueueName}\".", eventType, subscription.QueueName);
            await RejectAsync(delivery);
            return;
        }

        if (message is null)
        {
            logger.LogError("Empty \"{EventType}\" event received on queue \"{QueueName}\".", eventType, subscription.QueueName);
            await RejectAsync(delivery);
            return;
        }

        logger.LogInformation("Event \"{EventType}\" received on queue \"{QueueName}\".", eventType, subscription.QueueName);

        var error = Error.Empty;
        Exception? thrown = null;
        try
        {
            using var scope = serviceProvider.CreateScope();
            var currentUser = scope.ServiceProvider.GetRequiredService<CurrentUser>();
            currentUser.Metadata = ExtractMetadata(ea.BasicProperties.Headers);
            error = await subscription.Invoker(serviceProvider, currentUser, message);
        }
        catch (Exception e)
        {
            thrown = e;
            error = new Error(ErrorCode.Exception, e.ToString());
        }

        await AcknowledgeAsync(delivery, error, thrown);
    }

    private async Task AcknowledgeAsync(Delivery delivery, Error error, Exception? thrown)
    {
        var (channel, ea, subscription, activity, eventType) = delivery;

        if (!error.HasError)
        {
            await channel.BasicAckAsync(ea.DeliveryTag, false);
            RecordOutcome(delivery, MessagingOutcome.Processed);
            logger.LogInformation("Successfully processed \"{EventType}\" on queue \"{QueueName}\".", eventType, subscription.QueueName);
            return;
        }

        var retryCount = ReadRetryCount(ea.BasicProperties.Headers) + 1;
        if (error.Code != ErrorCode.Exception || retryCount > subscription.RetryCount)
        {
            await RejectAsync(delivery, error, thrown);
            return;
        }

        var retryProperties = CloneProperties(ea.BasicProperties, retryCount, eventType!);
        await channel.BasicPublishAsync(string.Empty, subscription.QueueName, true, retryProperties, ea.Body);
        await channel.BasicAckAsync(ea.DeliveryTag, false);

        activity?.SetTag("messaging.ifx.retry_count", retryCount);
        RecordOutcome(delivery, MessagingOutcome.Retried, error, thrown);
        logger.LogWarning("Failed to process \"{EventType}\" on queue \"{QueueName}\". Retrying, attempt {RetryCount} of {RetryLimit}. Error Code: {ErrorCode} message: {Error}",
            eventType, subscription.QueueName, retryCount, subscription.RetryCount, error.Code, error.Description);
    }

    private async Task RejectAsync(Delivery delivery, Error? error = null, Exception? thrown = null)
    {
        var subscription = delivery.Subscription;

        await delivery.Channel.BasicNackAsync(delivery.Args.DeliveryTag, false, false);
        RecordOutcome(delivery, RejectedOutcome(subscription), error, thrown);

        if (error is null)
        {
            delivery.Activity?.SetStatus(ActivityStatusCode.Error, "Rejected without requeue.");
            logger.LogError("Rejected a delivery of \"{EventType}\" on queue \"{QueueName}\". {Disposition}.",
                delivery.EventTypeTag, subscription.QueueName, Disposition(subscription));
            return;
        }

        logger.LogError("Failed to process \"{EventType}\" on queue \"{QueueName}\". {Disposition}. Error Code: {ErrorCode} message: {Error}",
            delivery.EventTypeTag, subscription.QueueName, Disposition(subscription), error.Code, error.Description);
    }

    private static string RejectedOutcome(Subscription subscription)
        => subscription.HasDeadLetterQueue ? MessagingOutcome.DeadLettered : MessagingOutcome.Discarded;

    private static string Disposition(Subscription subscription)
        => subscription.HasDeadLetterQueue
            ? $"Dead-lettered to \"{subscription.DeadLetterQueue}\""
            : "Discarded — no dead letter queue is configured for this subscriber";

    private static void RecordOutcome(Delivery delivery, string outcome, Error? error = null, Exception? thrown = null)
    {
        MessagingMeter.EventsConsumed.Add(1,
            new KeyValuePair<string, object?>("queue", delivery.Subscription.QueueName),
            new KeyValuePair<string, object?>("event_type", delivery.EventTypeTag),
            new KeyValuePair<string, object?>("outcome", outcome));

        var activity = delivery.Activity;
        if (activity is null) return;

        activity.SetTag("messaging.ifx.outcome", outcome);
        if (error is not { HasError: true }) return;

        activity.SetTag("error.type", thrown?.GetType().FullName ?? error.Code.ToString());
        activity.SetStatus(ActivityStatusCode.Error, error.Description);
        if (thrown is not null) activity.AddException(thrown);
    }

    private static string? ReadEventTypeHeader(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(MessagingHeaders.EventType, out var value))
            return null;

        return AsText(value);
    }

    private static int ReadRetryCount(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(MessagingHeaders.RetryCount, out var value))
            return 0;

        var count = value switch
        {
            int i => i,
            long l => l,
            short s => s,
            byte b => b,
            _ => long.TryParse(AsText(value), out var parsed) ? parsed : 0
        };

        return count is > 0 and < int.MaxValue ? (int)count : 0;
    }

    private static string? AsText(object? value) => value switch
    {
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        string text => text,
        _ => null
    };

    private static List<KeyValuePair<string, string>> ExtractMetadata(IDictionary<string, object?>? headers)
    {
        if (headers is null) return [];
        return headers
            .Where(header => !MessagingHeaders.All.Contains(header.Key))
            .SelectMany(header =>
            {
                var valueList = header.Value switch
                {
                    byte[] byteArray => DeserializeMetadataValues(byteArray),
                    _ => []
                };
                return valueList.Select(value => new KeyValuePair<string, string>(header.Key, value));
            }).ToList();
    }

    private static List<string> DeserializeMetadataValues(byte[] value)
    {
        try
        {
            return JsonSerializer.Deserialize<List<string>>(Encoding.UTF8.GetString(value)) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static Activity? StartConsumeActivity(BasicDeliverEventArgs ea, Subscription subscription)
    {
        var producerContext = ExtractProducerContext(ea.BasicProperties.Headers);
        ActivityLink[]? links = producerContext.TraceId == default ? null : [new ActivityLink(producerContext)];

        var activity = MessagingActivitySource.Source.StartActivity(
            $"process {subscription.QueueName}", ActivityKind.Consumer, parentContext: default, links: links);

        activity?.SetTag("messaging.system", "rabbitmq")
                .SetTag("messaging.operation.type", "process")
                .SetTag("messaging.destination.name", ea.Exchange)
                .SetTag("messaging.destination.subscription.name", subscription.QueueName)
                .SetTag("messaging.message.body.size", ea.Body.Length);

        return activity;
    }

    private static ActivityContext ExtractProducerContext(IDictionary<string, object?>? headers)
    {
        if (headers is null) return default;

        DistributedContextPropagator.Current.ExtractTraceIdAndState(headers,
            static (object? carrier, string fieldName, out string? value, out IEnumerable<string>? values) =>
            {
                values = null;
                value = carrier is IDictionary<string, object?> carrierHeaders && carrierHeaders.TryGetValue(fieldName, out var raw)
                    ? AsText(raw)
                    : null;
            },
            out var traceParent, out var traceState);

        return ActivityContext.TryParse(traceParent, traceState, out var context) ? context : default;
    }

    private static BasicProperties CloneProperties(IReadOnlyBasicProperties source, int retryCount, string eventType)
    {
        var headers = source.Headers is null
            ? []
            : new Dictionary<string, object?>(source.Headers);

        headers[MessagingHeaders.RetryCount] = retryCount;
        headers[MessagingHeaders.EventType] = Encoding.UTF8.GetBytes(eventType);

        return new BasicProperties
        {
            Persistent = source.Persistent,
            Headers = headers
        };
    }
}
