using Luizio.iFX.Models;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Luizio.iFX.Messaging;

public class RabbitMqPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly IConnectionFactory connectionFactory;
    private IConnection? connection = null;

    public RabbitMqPublisher(IConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<Response<Empty>> Publish<T>(T message, CurrentUser currentUser) where T : class, new()
    {
        return await Publish(message, string.Empty, currentUser);
    }

    public async Task<Response<Empty>> Publish<T>(T message, string routingKey, CurrentUser currentUser) where T : class, new()
    {
        if (connection == null || !connection.IsOpen)
        {
            connection = await connectionFactory.CreateConnectionAsync();
        }
        using var channel = await connection.CreateChannelAsync();


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

        var exchange = typeof(T).FullName!;
        await channel.BasicPublishAsync(exchange, routingKey, false, props, messageBodyBytes);

        return new Empty();
    }

    public ValueTask DisposeAsync()
    {
        return connection?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
