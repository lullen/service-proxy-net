using Luizio.ServiceProxy.Common;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System;

namespace Luizio.ServiceProxy.Messaging;
public static class MessagingExtensions
{
    public static ProxyBuilder AddMessaging(this ProxyBuilder services, MessagingSettings settings)
    {
        services.ServiceStore.RegisterSubscribers("pubsub");
        if (settings.MessagingType == MessagingType.RabbitMQ)
        {
            services.Services.AddHostedService<RabbitMqSubscriber>();
            services.Services.AddSingleton<IEventPublisher, RabbitMqPublisher>();
        }

        var connectionFactory = new ConnectionFactory
        {
            HostName = settings.Host,
            UserName = settings.Username,
            Password = settings.Password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(3)
        };
        services.Services.AddSingleton<IConnectionFactory>(connectionFactory);

        return services;
    }
}
