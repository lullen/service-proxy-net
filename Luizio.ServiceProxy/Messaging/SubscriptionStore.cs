using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using Luizio.ServiceProxy.Messaging;
using Luizio.ServiceProxy.Models;

namespace Luizio.ServiceProxy.Messaging;

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

        var s = new Subscription
        {
            ServiceType = typeof(TService),
            Service = typeof(TService).Name.ToLower(),
            Method = methodInfo,
            Topic = requestType.FullName!,
            DeadLetterQueue = settings.UseDeadLetterQueue ? $"{requestType.FullName}_dlq" : string.Empty,
            PubSub = settings.PubSub,
            PrefetchCount = settings.PrefetchCount,
            RetryCount = settings.RetryCount
        };
        _subscriptions.Add(s);
    }
}