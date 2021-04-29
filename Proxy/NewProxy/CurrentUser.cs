using System;
using System.Collections.Generic;
using System.Linq;

namespace Vera.Common
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
