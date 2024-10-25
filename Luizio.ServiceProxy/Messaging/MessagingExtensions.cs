using Luizio.ServiceProxy.Client;
using Luizio.ServiceProxy.Server;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Luizio.ServiceProxy.Messaging;
public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, MessagingSettings settings)
    {
        ServiceStore.RegisterSubscribers("pubsub");
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

        return services;
    }
}
