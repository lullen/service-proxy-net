using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Server;

namespace MessageTest;

public interface IMessageTest : IService
{
    Task<Response<Empty>> Message(MessageEvent message);
    Task<Response<Empty>> Try(MessageEvent message);
}

public class MessageEvent
{
    public Guid Id { get; set; }
}
