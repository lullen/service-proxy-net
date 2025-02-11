using Luizio.ServiceProxy.Models;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Luizio.ServiceProxy.Messaging;
public class RabbitMqPublisher : IEventPublisher
{
    private readonly IConnectionFactory connectionFactory;

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
        var connection = await connectionFactory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

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
}
