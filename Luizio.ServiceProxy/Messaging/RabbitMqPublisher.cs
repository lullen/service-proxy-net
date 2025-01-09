using Luizio.ServiceProxy.Models;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Luizio.ServiceProxy.Messaging;
public class RabbitMqPublisher : IEventPublisher
{
    private readonly IConnection connection;

    public RabbitMqPublisher(IConnectionFactory connectionFactory)
    {
        connection = connectionFactory.CreateConnection();
    }

    public Response<Empty> Publish<T>(T message, CurrentUser currentUser) where T : class, new()
    {
        return Publish(message, string.Empty, currentUser);
    }

    public Response<Empty> Publish<T>(T message, string routingKey, CurrentUser currentUser) where T : class, new()
    {
        var channel = connection.CreateModel();

        var messageBodyBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var props = channel.CreateBasicProperties();
        //props.ContentType = "text/json";
        props.Persistent = true;
        props.Headers = new Dictionary<string, object>();

        foreach (var metadata in currentUser.Metadata)
        {
            if (props.Headers.TryGetValue(metadata.Key, out var value))
            {
                var p = value as List<string> ?? new List<string>();
                p.Add(metadata.Value);
            }
            else
            {
                props.Headers.Add(metadata.Key, new List<string> { metadata.Value });

            }
        }
        var exchange = typeof(T).FullName;
        channel.BasicPublish(exchange, routingKey, props, messageBodyBytes);

        return new Empty();
    }
}
