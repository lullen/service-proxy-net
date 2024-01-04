using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Luizio.ServiceProxy.Models;

public class CurrentUser
{
    private const string AuthenticationHeader = "Authentication";
    private const string SubjectClaim = "sub";
    public Guid Id
    {
        get
        {
            if (Metadata.Any(m => m.Key == SubjectClaim) && Guid.TryParse(Metadata.Single(m => m.Key == SubjectClaim).Value, out var userId))
            {
                return userId;
            }
            return Guid.Empty;
        }
        set
        {
            if (Metadata.Any(m => m.Key == SubjectClaim))
            {
                var token = Metadata.SingleOrDefault(m => m.Key == SubjectClaim);
                Metadata.Remove(token);
            }
            Metadata.Add(new(SubjectClaim, value.ToString()));
        }
    }
    public List<KeyValuePair<string, string>> Metadata { get; set; } = new();
    public string Token
    {
        get
        {
            return Metadata.SingleOrDefault(y => y.Key == AuthenticationHeader).Value ?? string.Empty;
        }
        set
        {
            if (Metadata.Any(m => m.Key == AuthenticationHeader))
            {
                var token = Metadata.SingleOrDefault(m => m.Key == AuthenticationHeader);
                Metadata.Remove(token);
            }
            Metadata.Add(new(AuthenticationHeader, value));
        }
    }
    //public CurrentUser(Dictionary<string, object> metadata)
    //{
    //    Metadata = metadata;
    //}
    public CurrentUser() { }
}
