using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Luizio.ServiceProxy.Messaging;
public class MessagingSettings
{
    public string Host { get; set; } = string.Empty;
    public MessagingType MessagingType { get; set; }
}
