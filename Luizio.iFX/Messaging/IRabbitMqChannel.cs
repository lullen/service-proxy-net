using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Luizio.iFX.Messaging;

public interface IRabbitMqChannel : IAsyncDisposable
{
    IRabbitMqConsumer CreateConsumer();

    Task QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?>? arguments = null);
    Task QueueBindAsync(string queue, string exchange, string routingKey);
    Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global);
    Task<string> BasicConsumeAsync(string queue, bool autoAck, IRabbitMqConsumer consumer);
    ValueTask BasicAckAsync(ulong deliveryTag, bool multiple);
    ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue);
    ValueTask BasicPublishAsync(string exchange, string routingKey, bool mandatory, BasicProperties basicProperties, ReadOnlyMemory<byte> body);
    Task ExchangeDeclareAsync(string exchange, string type, bool durable);
}
