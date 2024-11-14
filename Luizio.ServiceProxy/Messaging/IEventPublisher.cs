using Luizio.ServiceProxy.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Luizio.ServiceProxy.Messaging;
public interface IEventPublisher
{
    Task<Response<Empty>> Publish<T>(T message, CurrentUser currentUser) where T : class, new();
}
