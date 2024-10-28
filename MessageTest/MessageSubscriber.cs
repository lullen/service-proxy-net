using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Server;

namespace MessageTest;

public class MessageSubscriber : IMessageTest, IService
{
    private readonly CurrentUser currentUser;

    public MessageSubscriber(CurrentUser currentUser)
    {
        this.currentUser = currentUser;
    }
    [Subscriber(useDeadLetterQueue: true, retryCount: 3)]
    public async Task<Response<Empty>> Message(MessageEvent message)
    {

        Console.WriteLine("Error Id: " + currentUser.Id);
        return await Task.FromResult(new Error(ErrorCode.Exception, "Error subscribing. Id: " + currentUser.Id));
        var rand = new Random();
        var next = rand.Next(1, 10);
        if (next <= 5)
        {
            Console.WriteLine("OK Id: " + currentUser.Id);
            return await Task.FromResult(new Empty());
        }
        else
        {
            Console.WriteLine("Error Id: " + currentUser.Id);
            return await Task.FromResult(new Error(ErrorCode.Exception, "Error subscribing. Id: " + currentUser.Id));
        }
    }

    //[Subscriber(useDeadLetterQueue: true)]
    public async Task<Response<Empty>> Try(MessageEvent message)
    {
        var rand = new Random();
        var next = rand.Next(1, 10);
        if (next <= 5)
        {
            return await Task.FromResult(new Empty());
        }
        else
        {
            return await Task.FromResult(new Error(ErrorCode.Exception, "Error subscribing. Id: " + currentUser.Id));
        }
    }
}
