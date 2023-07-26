
using System;

namespace Proxy.Server;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class ExposedServiceAttribute : Attribute
{

}