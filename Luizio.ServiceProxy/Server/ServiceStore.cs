using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Rewrite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Luizio.ServiceProxy.Server;

public class ServiceStore
{
    private readonly ILogger<ServiceStore> logger;
    private static Dictionary<string, Type> _services = new Dictionary<string, Type>();
    private static IEnumerable<Subscription> _subscriptions = new List<Subscription>();

    public ServiceStore(ILogger<ServiceStore> logger)
    {
        this.logger = logger;
    }

    public static IService GetService(string service, IServiceProvider provider)
    {
        var invokeClass = _services[service.ToLowerInvariant()];
        return (IService)provider.GetRequiredService(invokeClass);

    }

    public static MethodInfo GetMethod(string methodName, IService invokeClass)
    {
        MethodInfo? invokeMethod = null;
        methodName = methodName.ToLower();

        foreach (var method in invokeClass.GetType().GetMethods())
        {
            if (method.Name.ToLower() == methodName)
            {
                invokeMethod = method;
                break;
            }
        }

        if (invokeMethod == null)
        {
            throw new Exception("Method " + methodName + " not found");
        }
        return invokeMethod;
    }

    public static void RegisterServices(params Type[] services)
    {
        foreach (var service in services)
        {
            _services.Add(service.Name.ToLower(), service);
        }
        // _logger.info("Registered {} services.", _services.size());
    }

    public static void RegisterService(Type service)
    {
        _services.Add(service.Name.ToLower(), service);
        //logger.info("Registered {} as a service.", service.Name);
    }

    public static IEnumerable<Subscription> GetSubscriptions()
    {
        return _subscriptions;
    }

    public static void RegisterSubscribers(string pubsub)
    {

        foreach (var item in _services)
        {
            foreach (var method in item.Value.GetMethods())
            {
                var subscriber = method.GetCustomAttribute<SubscriberAttribute>();
                if (subscriber != null)
                {
                    var s = new Subscription
                    {
                        Method = item.Key + "." + method.Name,
                        Topic = subscriber.Topic,
                        PubSub = pubsub
                    };
                }
            }
        }
        // _logger.info("Registered {} topics", _subscriptions.size());
    }
}