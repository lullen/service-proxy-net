using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Luizio.iFX.Messaging;

internal class ExchangeInitializer(IConnectionFactory connectionFactory, ILogger<ExchangeInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            var eventTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .Where(t => t.IsClass && !t.IsAbstract && typeof(IEvent).IsAssignableFrom(t));

            await using var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
            await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

            foreach (var type in eventTypes)
            {
                await channel.ExchangeDeclareAsync(exchange: type.FullName!, durable: true, type: ExchangeType.Fanout, cancellationToken: cancellationToken);
                logger.LogInformation("Declared exchange {Exchange}.", type.FullName);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to declare exchanges.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
