using RabbitMQ.Client.Events;
using System.Threading.Tasks;

namespace Luizio.iFX.Messaging;

public interface IRabbitMqConsumer
{
    event AsyncEventHandler<BasicDeliverEventArgs>? ReceivedAsync;
}
