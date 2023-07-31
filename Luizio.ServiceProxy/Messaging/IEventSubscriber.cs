using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Luizio.ServiceProxy.Messaging;

public interface IEventSubscriber
{
    void Connect();
    void Subscribe();
}

public record EventSubscription<T>(string Queue, string? DeadLetterQueue = null) { }