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
    SubscriberSettings settings,
    Type[]? bindTo = null)
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
        var (boundExchanges, typesByExchange) = BuildBindings(requestType, bindTo);
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
            QueueTopic = requestType.FullName!,
            BoundExchanges = boundExchanges,
            TypesByExchange = typesByExchange,
            DeadLetterQueue = settings.UseDeadLetterQueue ? $"{requestType.FullName}_dlq" : string.Empty,
            PubSub = settings.PubSub,
            PrefetchCount = settings.PrefetchCount,
            RetryCount = settings.RetryCount
        };

        var clash = _subscriptions.FirstOrDefault(existing => existing.QueueName == s.QueueName);
        if (clash is not null)
            throw new ArgumentException(
                $"Queue \"{s.QueueName}\" is already registered for " +
                $"{clash.Service}.{clash.MethodName}. Two subscriptions sharing a queue become " +
                "competing consumers, so each message would reach only one of them.");

        _subscriptions.Add(s);
    }

    private static (List<string> BoundExchanges, Dictionary<string, Type> TypesByExchange) BuildBindings(
        Type parameterType, Type[]? bindTo)
    {
        if (bindTo is null)
        {
            if (!parameterType.IsClass || parameterType.IsAbstract)
                throw new ArgumentException(
                    $"Subscriber parameter \"{parameterType.FullName}\" is not a concrete event type. " +
                    "Pass bindTo listing the concrete event types this handler should receive.");

            RequireEvent(parameterType);
            var topic = parameterType.FullName!;
            return ([topic], new Dictionary<string, Type> { [topic] = parameterType });
        }

        if (bindTo.Length == 0)
            throw new ArgumentException(
                "bindTo was supplied but empty. The subscription would bind to no exchange and " +
                "receive nothing. Omit bindTo to bind to the parameter type's own exchange.");

        var exchanges = new List<string>(bindTo.Length);
        var typesByExchange = new Dictionary<string, Type>(bindTo.Length);
        foreach (var type in bindTo)
        {
            if (!type.IsClass || type.IsAbstract)
                throw new ArgumentException(
                    $"bindTo type \"{type.FullName}\" must be a non-abstract class — only a concrete " +
                    "event type names an exchange and can be deserialized into.");

            if (!parameterType.IsAssignableFrom(type))
                throw new ArgumentException(
                    $"bindTo type \"{type.FullName}\" is not assignable to subscriber parameter " +
                    $"\"{parameterType.FullName}\" and could not be passed to the handler.");

            RequireEvent(type);

            var exchange = type.FullName
                ?? throw new ArgumentException($"bindTo type \"{type}\" has no full name and cannot name an exchange.");

            if (!typesByExchange.TryAdd(exchange, type))
                throw new ArgumentException($"bindTo lists \"{exchange}\" more than once.");

            exchanges.Add(exchange);
        }

        return (exchanges, typesByExchange);
    }

    private static void RequireEvent(Type type)
    {
        if (!typeof(IEvent).IsAssignableFrom(type))
            throw new ArgumentException(
                $"Event type \"{type.FullName}\" must implement {nameof(IEvent)}. Only an {nameof(IEvent)} " +
                "can be published, so a subscription on anything else could never receive a message.");
    }

    private static Func<IServiceProvider, CurrentUser, object, Task<Error>> CreateInvoker<TService, TParam, TRes>(
        string appName, string serviceName, string methodName)
        where TService : class, IService
        where TParam : class
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