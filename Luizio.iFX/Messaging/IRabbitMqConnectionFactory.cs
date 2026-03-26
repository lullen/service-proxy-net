using System.Threading;
using System.Threading.Tasks;

namespace Luizio.iFX.Messaging;

public interface IRabbitMqConnectionFactory
{
    Task<IRabbitMqConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
