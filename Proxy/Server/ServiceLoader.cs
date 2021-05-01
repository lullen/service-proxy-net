using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Proxy.Server
{
    public class ServiceLoader
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private static Dictionary<string, Type> _services = new Dictionary<string, Type>();
        private static IEnumerable<Subscription> _subscriptions = new List<Subscription>();

        public ServiceLoader(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        public IServiceScope CreateScope()
        {
            return _scopeFactory.CreateScope();
        }

        public IService Create(string method, IServiceScope scope)
        {
            var className = method.Substring(0, method.LastIndexOf(".")).ToLower();
            var invokeClass = _services[className];
            return (IService)scope.ServiceProvider.GetRequiredService(invokeClass);
        }

        public MethodInfo GetMethod(String methodName, IService invokeClass)
        {
            MethodInfo? invokeMethod = null;

            if (methodName.Contains("."))
            {
                methodName = methodName.Substring(methodName.LastIndexOf(".") + 1);
            }

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
                var exposedService = service.GetCustomAttribute<ExposedServiceAttribute>();
                if (exposedService != null)
                {
                    _services.Add(service.Name.ToLower(), service);
                }
            }
            // _logger.info("Registered {} services.", _services.size());
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
                        var s = new Subscription();
                        s.Method = item.Key + "." + method.Name;
                        s.Topic = subscriber.Topic;
                        s.PubSub = pubsub;
                    }
                }
            }
            // _logger.info("Registered {} topics", _subscriptions.size());
        }
    }
}