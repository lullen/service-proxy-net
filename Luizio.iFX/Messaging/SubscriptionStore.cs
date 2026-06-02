using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Luizio.iFX.Client;
using Luizio.iFX.Messaging;
using Luizio.iFX.Models;
using Luizio.iFX.Server;

namespace Luizio.iFX.Messaging;

internal class SubscriptionStore
{
    private IList<Subscription> _subscriptions = [];

    internal IEnumerable<Subscription> GetSubscriptions()
    {
        return _subscriptions;
    }

    internal void RegisterSubscriber<TService>(
    Expression<Func<TService, Delegate>> methodSelector,
    SubscriberSettings settings)
    where TService : class, IService
    {
        var unaryExpression = methodSelector.Body as UnaryExpression;
        var methodCallExpression = unaryExpression!.Operand as MethodCallExpression;
        var constantExpression = methodCallExpression!.Object as ConstantExpression;
        var methodInfo = constantExpression!.Value as MethodInfo;

        var parameters = methodInfo.GetParameters();
        if (parameters.Length != 1)
            throw new ArgumentException("Subscriber method must take exactly one parameter");

        var returnType = methodInfo.ReturnType;
        if (!returnType.IsGenericType || returnType.GetGenericTypeDefinition() != typeof(Task<>))
            throw new ArgumentException("Subscriber method must return Task<Response<T>>");

        var innerReturn = returnType.GetGenericArguments()[0];
        if (!innerReturn.IsGenericType || innerReturn.GetGenericTypeDefinition() != typeof(Response<>))
            throw new ArgumentException("Subscriber method must return Task<Response<T>>");

        var requestType = parameters[0].ParameterType;
        var tRes = innerReturn.GetGenericArguments()[0];
        var serviceName = typeof(TService).Name.ToLower();
        var appName = typeof(TService).Assembly.GetName().Name ?? typeof(TService).Name;

        var invoker = (Func<IServiceProvider, CurrentUser, object, Task<Error>>)
            typeof(SubscriptionStore)
                .GetMethod(nameof(CreateInvoker), BindingFlags.NonPublic | BindingFlags.Static)!
                .MakeGenericMethod(typeof(TService), requestType, tRes)
                .Invoke(null, [appName, serviceName, methodInfo.Name])!;

        var s = new Subscription
        {
            Invoker = invoker,
            EventType = requestType,
            MethodName = methodInfo.Name,
            Service = serviceName,
            Topic = requestType.FullName!,
            DeadLetterQueue = settings.UseDeadLetterQueue ? $"{requestType.FullName}_dlq" : string.Empty,
            PubSub = settings.PubSub,
            PrefetchCount = settings.PrefetchCount,
            RetryCount = settings.RetryCount
        };
        _subscriptions.Add(s);
    }

    private static Func<IServiceProvider, CurrentUser, object, Task<Error>> CreateInvoker<TService, TParam, TRes>(
        string appName, string serviceName, string methodName)
        where TService : class, IService
        where TParam : class, new()
        where TRes : class
    {
        return async (sp, cu, msg) =>
        {
            var proxy = new InProcServiceProxy<TService>(sp, cu);
            var response = await proxy.Invoke<TParam, TRes>(appName, serviceName, methodName, (TParam)msg);
            return response.Error;
        };
    }
}