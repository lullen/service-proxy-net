using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Luizio.ServiceProxy.Server;

internal class ServiceStore
{
    private Dictionary<string, Type> _services = [];
    private IList<Subscription> _subscriptions = [];

    internal void Clear()
    {
        _services = new Dictionary<string, Type>();
        _subscriptions = new List<Subscription>();
    }

    internal IService GetService(string service, IServiceProvider provider)
    {
        var invokeClass = _services[service.ToLowerInvariant()];
        return (IService)provider.GetRequiredService(invokeClass);

    }

    internal MethodInfo GetMethod(string methodName, Type parameterType, IService invokeClass)
    {
        MethodInfo? invokeMethod = null;
        methodName = methodName.ToLower();

        foreach (var method in invokeClass.GetType().GetMethods())
        {
            if (method.Name.ToLower() == methodName && method.GetParameters().FirstOrDefault()?.ParameterType == parameterType)
            {
                invokeMethod = method;
                break;
            }
        }

        if (invokeMethod == null)
        {
            throw new Exception($"Method \"{invokeClass.GetType().Name}.{methodName}\" with parameter \"{parameterType}\" not found.");
        }
        return invokeMethod;
    }

    internal void RegisterService(Type service)
    {
        _services.Add(service.Name.ToLower(), service);
        //logger.info("Registered {} as a service.", service.Name);
    }

    internal IEnumerable<Subscription> GetSubscriptions()
    {
        return _subscriptions;
    }

    internal void RegisterSubscribers(string pubsub)
    {
        foreach (var item in _services)
        {
            var interfaces = item.Value.GetInterfaces();
            foreach (var method in item.Value.GetMethods())
            {
                var subscriber = method.GetCustomAttribute<SubscriberAttribute>();
                if (subscriber == null)
                {
                    continue;
                }

                var declaringInterface = interfaces
                    .Where(i =>
                        i.GetMethods().Any(m =>
                            m.Name == method.Name &&
                            m.GetParameters().All(p => method.GetParameters().Select(pm => pm.ParameterType).Contains(p.ParameterType))
                        )
                    ).FirstOrDefault() ?? throw new InvalidOperationException($"Can only subscribe to methods declared in interfaces. Method {method.Name} in {item.Value.Name} is not an implementation of an interface.");
                if (method.GetParameters().Length != 1)
                {
                    throw new InvalidOperationException("Can only subscribe to methods with one parameter.");
                }

                var s = new Subscription
                {
                    Service = item.Key,
                    Method = method,
                    Topic = method.GetParameters().First().ParameterType.FullName!.ToString(),
                    DeadLetterQueue = subscriber.UseDeadLetterQueue ? $"{method.GetParameters().First().ParameterType.FullName}_dlq" : string.Empty,
                    PubSub = pubsub,
                    PrefetchCount = subscriber.PrefetchCount,
                    RetryCount = subscriber.RetryCount
                };
                _subscriptions.Add(s);
            }
        }
        // _logger.info("Registered {} topics", _subscriptions.size());
    }
}