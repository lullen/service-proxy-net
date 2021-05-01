using System;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Proxy.Models;
using Proxy.Server;

namespace Proxy.Client
{
    public class ServiceProxy<T> : DispatchProxy where T : class, IService
    {
        private IServiceScopeFactory? _sp;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            _ = targetMethod ?? throw new System.Exception("Target method is null");
            _ = _sp ?? throw new System.Exception("ServiceProvider is null");

            if (args == null || args.Length == 0 || args[0] == null)
            {
                throw new System.Exception("Must have exactly one argument!");
            }

            if (!IsTaskOfResponse(targetMethod.ReturnType))
            {
                throw new System.Exception("Return type must be of type Task<Response<T>>!");
            }

            using var scope = _sp.CreateScope();
            var proxy = scope.ServiceProvider.GetRequiredService<IServiceProxy>();

            var invoke = proxy.GetType().GetMethod("Invoke");
            var type1 = args[0]!.GetType();
            var type2 = targetMethod!.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0];
            var m = invoke!.MakeGenericMethod(type1, type2);

            var methodSplit = targetMethod.DeclaringType!.FullName!.LastIndexOf(".");
            var app = targetMethod.DeclaringType.FullName.Substring(0, methodSplit).Replace(".Interfaces", "");
            var methodName = $"{targetMethod.DeclaringType.Name}.{targetMethod.Name}";

            return m.Invoke(proxy, new[] { app, methodName, args[0] });
        }

        private bool IsTaskOfResponse(Type type)
        {
            if (type.GetGenericTypeDefinition() != typeof(Task<>))
            {
                return false;
            }

            if (type.GetGenericArguments()[0].GetGenericTypeDefinition() != typeof(Response<>))
            {
                return false;
            }

            return true;
        }

        public static T Create(IServiceScopeFactory sp)
        {
            object proxy = Create<T, ServiceProxy<T>>();
            var serviceProxy = (ServiceProxy<T>)proxy;
            serviceProxy._sp = sp;
            return (T)proxy;
        }
    }

    public class ServiceProxy
    {
        private readonly IServiceScopeFactory _sp;
        public ServiceProxy(IServiceScopeFactory sp)
        {
            _sp = sp;
        }

        public T Create<T>() where T : class, IService
        {
            object proxy = ServiceProxy<T>.Create(_sp);
            return (T)proxy;
        }
    }
}