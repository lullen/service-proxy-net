using Luizio.iFX.Models;
using RabbitMQ.Client;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Luizio.iFX.Messaging;

public class RabbitMqPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IRabbitMqConnectionFactory connectionFactory;
    private IRabbitMqConnection? connection = null;
    private readonly ConcurrentDictionary<string, bool> declaredExchanges = new();

    public RabbitMqPublisher(IRabbitMqConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<Response<Empty>> Publish<T>(T message, CurrentUser currentUser) where T : class, IEvent, new()
    {
        return await Publish(message, string.Empty, currentUser);
    }

    public async Task<Response<Empty>> Publish<T>(T message, string routingKey, CurrentUser currentUser) where T : class, IEvent, new()
    {
        var exchange = message.GetType().FullName!;

        using var activity = MessagingActivitySource.Source.StartActivity(
            $"publish {exchange}", ActivityKind.Producer);
        activity?.SetTag("messaging.system", "rabbitmq")
                .SetTag("messaging.operation.type", "publish")
                .SetTag("messaging.destination.name", exchange)
                .SetTag("messaging.message.type", exchange);

        if (connection == null || !connection.IsOpen)
        {
            connection = await connectionFactory.CreateConnectionAsync();
        }
        await using var channel = await connection.CreateChannelAsync();

        if (!declaredExchanges.ContainsKey(exchange))
        {
            await channel.ExchangeDeclareAsync(exchange, ExchangeType.Fanout, durable: true);
            declaredExchanges[exchange] = true;
        }

        var messageBodyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));

        var props = new BasicProperties
        {
            Persistent = true,
            Headers = new Dictionary<string, object?>()
        };

        var testing = new Dictionary<string, List<string>>();
        foreach (var metadata in currentUser.Metadata)
        {
            if (testing.TryGetValue(metadata.Key, out var value))
            {
                var p = value ?? [];
                p.Add(metadata.Value);
            }
            else
            {
                testing.Add(metadata.Key, [metadata.Value]);
            }
        }

        foreach (var key in testing.Keys)
        {
            props.Headers.Add(key, JsonSerializer.Serialize(testing[key]));
        }

        props.Headers[MessagingHeaders.EventType] = Encoding.UTF8.GetBytes(exchange);
        InjectTraceContext(activity, props.Headers);

        await channel.BasicPublishAsync(exchange, routingKey, false, props, messageBodyBytes);

        MessagingMeter.EventsPublished.Add(1, new KeyValuePair<string, object?>("exchange", exchange));

        return new Empty();
    }

    private static void InjectTraceContext(Activity? activity, IDictionary<string, object?> headers)
    {
        if (activity is null) return;

        DistributedContextPropagator.Current.Inject(activity, headers, static (carrier, key, value) =>
        {
            if (carrier is IDictionary<string, object?> headers)
                headers[key] = Encoding.UTF8.GetBytes(value);
        });
    }

    public ValueTask DisposeAsync()
    {
        return connection?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
