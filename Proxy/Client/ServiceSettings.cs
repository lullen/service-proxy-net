using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Proxy.Client;

public class ServiceSettings
{
    public Dictionary<string, string> Services { get; set; } = new();
}
