using System;
using System.Threading;
using System.Threading.Tasks;

namespace Luizio.iFX.Messaging;

public interface IRabbitMqConnection : IAsyncDisposable
{
    bool IsOpen { get; }
    Task<IRabbitMqChannel> CreateChannelAsync();
    Task CloseAsync(CancellationToken cancellationToken = default);
}
