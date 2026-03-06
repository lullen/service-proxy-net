
using System;
using Luizio.iFX.Messaging;
using Microsoft.Extensions.DependencyInjection;

namespace Luizio.iFX.Common;

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