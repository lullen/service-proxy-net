using System;
using System.Collections.Generic;
using System.Linq;

namespace Proxy.Client
{
    public class CurrentUser
    {
        public string Token { get; private set; }
        public CurrentUser(string token)
        {
            Token = token;
        }
    }
}
