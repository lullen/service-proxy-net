using Luizio.ServiceProxy.Client;
using Luizio.ServiceProxy.Server;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Luizio.ServiceProxy.Messaging;
public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging(this IServiceCollection services, MessagingType messagingType)
    {
        ServiceStore.RegisterSubscribers("pubsub");
        if (messagingType == MessagingType.RabbitMQ)
        {
            services.AddHostedService<RabbitMqSubscriber>();
        }
        services.AddSingleton<IEventPublisher, RabbitMqPublisher>();

        return services;
    }
}
