
using System;
using Luizio.ServiceProxy.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Luizio.ServiceProxy.Common;

public class MessagingBuilder
{
    internal readonly IServiceCollection Services;
    internal readonly SubscriptionStore ServiceStore;

    internal MessagingBuilder(IServiceCollection services, SubscriptionStore serviceStore)
    {
        Services = services;
        ServiceStore = serviceStore;
    }
}