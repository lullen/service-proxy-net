using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Proxy.Client;

public class CurrentUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Dictionary<string, StringValues> Metadata { get; set; } = new();
    public string Token { get; set; } = string.Empty;
    //public CurrentUser(Dictionary<string, object> metadata)
    //{
    //    Metadata = metadata;
    //}
    public CurrentUser() { }
}
