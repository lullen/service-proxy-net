
using Luizio.ServiceProxy.Client;
using Luizio.ServiceProxy.Messaging;

namespace MessageTest;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddProxyClient(ProxyType.InProc)
            .AddService<MessageSubscriber>()
            .AddMessaging(MessagingType.RabbitMQ);

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthorization();


        app.MapControllers();
        app.MapGet("/", () =>
        {
            return "hello world";
        });
        app.Run();
    }
}
