using Luizio.ServiceProxy.Server;
using Microsoft.Extensions.DependencyInjection;
using System.Formats.Asn1;
using System.Reflection;

namespace Luizio.ServiceProxy.Testing;

public static class ServiceProxyExtensions
{
    public static IServiceCollection AddMockService<T>(this IServiceCollection services, T mockObject, string name) where T : class
    {
        Type type = typeof(ServiceStore);
        FieldInfo info = type.GetField("_services", BindingFlags.NonPublic | BindingFlags.Static);
        var serviceDictionary = info.GetValue(null) as Dictionary<string, Type>;
        serviceDictionary.Add(name.ToLower(), typeof(T));
        services.AddSingleton(mockObject);
        return services;
    }
}
