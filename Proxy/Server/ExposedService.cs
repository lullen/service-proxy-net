
using System;

namespace Proxy.Server
{
    [AttributeUsage(AttributeTargets.Interface, Inherited = false, AllowMultiple = true)]
    public class ExposedServiceAttribute : Attribute
    {

    }
}