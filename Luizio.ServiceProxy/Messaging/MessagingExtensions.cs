using Luizio.ServiceProxy.Common;
using Luizio.ServiceProxy.Models;
using Luizio.ServiceProxy.Server;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;

namespace Luizio.ServiceProxy.Messaging;

public static class MessagingExtensions
{
    public static MessagingBuilder AddMessaging(this IServiceCollection services, MessagingSettings settings)
    {
        if (settings.MessagingType == MessagingType.RabbitMQ)
        {
            services.AddHostedService<RabbitMqSubscriber>();
            services.AddSingleton<IEventPublisher, RabbitMqPublisher>();
        }

        var connectionFactory = new ConnectionFactory
        {
            HostName = settings.Host,
            UserName = settings.Username,
            Password = settings.Password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(3)
        };
        services.AddSingleton<IConnectionFactory>(connectionFactory);

        var serviceStore = new SubscriptionStore();
        services.AddSingleton(serviceStore);
        return new MessagingBuilder(services, serviceStore);
    }

    public static MessagingBuilder RegisterSubscriber<TService>(this MessagingBuilder services, Expression<Func<TService, Delegate>> methodSelector, SubscriberSettings settings) where TService : class, IService
    {
        if (!services.Services.Any(a => a.IsKeyedService && a.ServiceType == typeof(TService)))
        {
            throw new Exception($"Service {typeof(TService).Name} not registered and cannot therefore be used for subscribers.");
        }
        if (string.IsNullOrEmpty(settings.PubSub))
        {
            settings.PubSub = "pubsub";
        }
        services.ServiceStore.RegisterSubscriber<TService>(methodSelector, settings);

        return services;
    }
}
