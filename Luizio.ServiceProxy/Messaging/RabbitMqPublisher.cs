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

    public RabbitMqPublisher()
    {
        var connectionFactory = new ConnectionFactory
        {
            HostName = "localhost",
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(3)
        };
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
        props.Headers = new Dictionary<string,object>();

        foreach (var metadata in currentUser.Metadata)
        {
            props.Headers.Add(metadata.Key, metadata.Value);
        }
        var exchange = typeof(T).FullName;
        channel.BasicPublish(exchange, routingKey, props, messageBodyBytes);

        return new Empty();
    }
}
