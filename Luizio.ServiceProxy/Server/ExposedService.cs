
using System;

namespace Luizio.ServiceProxy.Server;

[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
public class ExposedServiceAttribute : Attribute
{

}