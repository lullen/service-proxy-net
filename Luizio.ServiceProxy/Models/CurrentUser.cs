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
            if (Metadata.TryGetValue(SubjectClaim, out var id) && Guid.TryParse(id, out var userId))
            {
                return userId;
            }
            return Guid.Empty;
        }
        set
        {
            if (Metadata.ContainsKey(SubjectClaim))
            {
                Metadata[AuthenticationHeader] = value.ToString();
            }
            else
            {
                Metadata.Add(SubjectClaim, value.ToString());
            }
        }
    }
    public Dictionary<string, string> Metadata { get; set; } = new();
    public string Token
    {
        get
        {
            Metadata.TryGetValue(AuthenticationHeader, out var token);
            return token != null ? token : string.Empty;
        }
        set
        {
            if (Metadata.ContainsKey(AuthenticationHeader))
            {
                Metadata[AuthenticationHeader] = value;
            }
            else
            {
                Metadata.Add(AuthenticationHeader, value);
            }
        }
    }
    //public CurrentUser(Dictionary<string, object> metadata)
    //{
    //    Metadata = metadata;
    //}
    public CurrentUser() { }
}
