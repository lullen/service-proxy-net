using Luizio.ServiceProxy.Messaging;
using Luizio.ServiceProxy.Models;
using Microsoft.AspNetCore.Mvc;

namespace MessageTest.Controllers;
[ApiController]
[Route("[controller]")]
public class TestController : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };

    private readonly ILogger<TestController> _logger;
    private readonly IEventPublisher eventPublisher;
    private readonly CurrentUser currentUser;

    public TestController(ILogger<TestController> logger, IEventPublisher eventPublisher, CurrentUser currentUser)
    {
        _logger = logger;
        this.eventPublisher = eventPublisher;
        this.currentUser = currentUser;
    }

    [HttpGet(Name = "GetWeatherForecast")]
    public IEnumerable<WeatherForecast> Get()
    {
        currentUser.Metadata.Add(KeyValuePair.Create("Test", "Test value"));
        eventPublisher.Publish(new MessageEvent { Id = Guid.NewGuid() }, currentUser);

        return Enumerable.Range(1, 5).Select(index => new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        })
        .ToArray();
    }
}
