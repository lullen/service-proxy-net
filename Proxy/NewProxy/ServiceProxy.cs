using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Proxy.NewProxy
{
    public class ServiceProxy<T> : DispatchProxy where T : class, IService
    {
        private IServiceScopeFactory? _sp;
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (args == null || args.Length == 0)
            {
                throw new System.Exception("Must have exactly one argument!");
            }
            using var scope = _sp.CreateScope();
            var proxy = scope.ServiceProvider.GetRequiredService<IServiceProxy>();

            var invoke = proxy.GetType().GetMethod("Invoke");
            var type1 = args[0].GetType();
            var type2 = targetMethod.ReturnType.GetGenericArguments()[0].GetGenericArguments()[0];
            var m = invoke.MakeGenericMethod(type1, type2);




            var methodSplit = targetMethod.DeclaringType.FullName.LastIndexOf(".");
            var app = targetMethod.DeclaringType.FullName.Substring(0, methodSplit).Replace(".Interfaces", "");
            // var clazz = targetMethod.DeclaringType.FullName.Substring(0, methodSplit);
            var methodName = $"{targetMethod.DeclaringType.Name}.{targetMethod.Name}";


            return m.Invoke(proxy, new[] { app, methodName, args[0] });
        }

        public static T Create(IServiceScopeFactory sp)
        {
            object proxy = Create<T, ServiceProxy<T>>();
            var serviceProxy = (ServiceProxy<T>)proxy;
            serviceProxy._sp = sp;
            // serviceProxy.proxiedObject = decorated;
            // serviceProxy.logger = logger;

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