using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Luizio.iFX.Messaging;

internal sealed class RabbitMqConnectionFactoryWrapper(IConnectionFactory inner) : IRabbitMqConnectionFactory
{
    public async Task<IRabbitMqConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
        => new RabbitMqConnectionWrapper(await inner.CreateConnectionAsync(cancellationToken));
}

internal sealed class RabbitMqConnectionWrapper(IConnection inner) : IRabbitMqConnection
{
    public bool IsOpen => inner.IsOpen;

    public async Task<IRabbitMqChannel> CreateChannelAsync()
        => new RabbitMqChannelWrapper(await inner.CreateChannelAsync());

    public Task CloseAsync(CancellationToken cancellationToken = default)
        => inner.CloseAsync(cancellationToken);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

internal sealed class RabbitMqChannelWrapper(IChannel inner) : IRabbitMqChannel
{
    public IRabbitMqConsumer CreateConsumer() => new RabbitMqConsumerWrapper(inner);

    public async Task QueueDeclareAsync(string queue, bool durable, bool exclusive, bool autoDelete, IDictionary<string, object?>? arguments = null)
        => await inner.QueueDeclareAsync(queue, durable, exclusive, autoDelete, arguments);

    public Task QueueBindAsync(string queue, string exchange, string routingKey)
        => inner.QueueBindAsync(queue, exchange, routingKey);

    public Task BasicQosAsync(uint prefetchSize, ushort prefetchCount, bool global)
        => inner.BasicQosAsync(prefetchSize, prefetchCount, global);

    public Task<string> BasicConsumeAsync(string queue, bool autoAck, IRabbitMqConsumer consumer)
        => inner.BasicConsumeAsync(queue, autoAck, consumer: (RabbitMqConsumerWrapper)consumer);

    public ValueTask BasicAckAsync(ulong deliveryTag, bool multiple)
        => inner.BasicAckAsync(deliveryTag, multiple);

    public ValueTask BasicNackAsync(ulong deliveryTag, bool multiple, bool requeue)
        => inner.BasicNackAsync(deliveryTag, multiple, requeue);

    public ValueTask BasicPublishAsync(string exchange, string routingKey, bool mandatory, BasicProperties basicProperties, ReadOnlyMemory<byte> body)
        => inner.BasicPublishAsync(exchange, routingKey, mandatory, basicProperties, body);

    public Task ExchangeDeclareAsync(string exchange, string type, bool durable)
        => inner.ExchangeDeclareAsync(exchange, durable: durable, type: type);

    public ValueTask DisposeAsync() => inner.DisposeAsync();
}

internal sealed class RabbitMqConsumerWrapper(IChannel channel) : IRabbitMqConsumer, IAsyncBasicConsumer
{
    private readonly AsyncEventingBasicConsumer _inner = new(channel);

    public event AsyncEventHandler<BasicDeliverEventArgs>? ReceivedAsync
    {
        add => _inner.ReceivedAsync += value;
        remove => _inner.ReceivedAsync -= value;
    }

    // IAsyncBasicConsumer — delegate to the inner consumer
    IChannel IAsyncBasicConsumer.Channel => _inner.Channel;
    Task IAsyncBasicConsumer.HandleBasicCancelAsync(string consumerTag, CancellationToken ct) => _inner.HandleBasicCancelAsync(consumerTag, ct);
    Task IAsyncBasicConsumer.HandleBasicCancelOkAsync(string consumerTag, CancellationToken ct) => _inner.HandleBasicCancelOkAsync(consumerTag, ct);
    Task IAsyncBasicConsumer.HandleBasicConsumeOkAsync(string consumerTag, CancellationToken ct) => _inner.HandleBasicConsumeOkAsync(consumerTag, ct);
    Task IAsyncBasicConsumer.HandleBasicDeliverAsync(string consumerTag, ulong deliveryTag, bool redelivered, string exchange, string routingKey, IReadOnlyBasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken ct) =>
        _inner.HandleBasicDeliverAsync(consumerTag, deliveryTag, redelivered, exchange, routingKey, properties, body, ct);
    Task IAsyncBasicConsumer.HandleChannelShutdownAsync(object channel, ShutdownEventArgs reason) => _inner.HandleChannelShutdownAsync(channel, reason);
}
