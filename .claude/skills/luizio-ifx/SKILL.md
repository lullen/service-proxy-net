---
name: luizio-ifx
description: Guide for using Luizio.iFX — the service proxy and messaging framework in this repo. Covers hosting a service, calling it via proxy (HTTP or InProc), and publishing/subscribing to events over RabbitMQ.
---

You are helping a developer use the Luizio.iFX framework in this repo. Provide accurate, concrete guidance based on the framework's actual API.

## Overview

Luizio.iFX enables transparent remote service invocation (HTTP or in-process) and RabbitMQ-based event messaging. Services are defined as interfaces, implemented in a server project, and consumed by clients through a generated proxy — no direct implementation references needed.

**All proxied methods must return `Task<Response<T>>`.**

---

## Part 1: Hosting a Service (Server Side)

### 1. Define the interface (in a shared Interfaces project)

```csharp
// Server.Interfaces/IOrderService.cs
public interface IOrderService : IService
{
    Task<Response<OrderResult>> PlaceOrder(PlaceOrderRequest request);
    Task<Response<Empty>> CancelOrder(CancelOrderRequest request);
}

public class PlaceOrderRequest
{
    public Guid CustomerId { get; set; }
    public List<string> Items { get; set; } = [];
}

public class OrderResult
{
    public Guid OrderId { get; set; }
}

public class CancelOrderRequest
{
    public Guid OrderId { get; set; }
}
```

### 2. Implement the interface (in the Server project)

```csharp
// Server/OrderService.cs
public class OrderService : IOrderService
{
    private readonly CurrentUser currentUser;
    private readonly ILogger<OrderService> logger;

    public OrderService(CurrentUser currentUser, ILogger<OrderService> logger)
    {
        this.currentUser = currentUser;
        this.logger = logger;
    }

    public Task<Response<OrderResult>> PlaceOrder(PlaceOrderRequest request)
    {
        logger.LogInformation("Order placed by user {UserId}", currentUser.Id);
        var result = new OrderResult { OrderId = Guid.NewGuid() };
        return Task.FromResult<Response<OrderResult>>(result);
    }

    public Task<Response<Empty>> CancelOrder(CancelOrderRequest request)
    {
        if (request.OrderId == Guid.Empty)
            return Task.FromResult<Response<Empty>>(new Error(ErrorCode.InvalidInput, "OrderId is required"));

        return Task.FromResult<Response<Empty>>(new Empty());
    }
}
```

### 3. Host it in an ASP.NET Core app

```csharp
// HttpServer/Program.cs
using Luizio.iFX.Server;
using Server;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddTransient<OrderService>();
builder.Services.AddProxyServer();

var app = builder.Build();
app.MapService<OrderService>();  // Registers POST /{ServiceName}/{MethodName} routes
app.Run();
```

`MapService<T>()` automatically exposes each method as `POST /OrderService/PlaceOrder`, etc.

---

## Part 2: Calling a Service via Proxy (Client Side)

### 1. Configure appsettings.json

```json
{
  "ProxyType": "HTTP",
  "Services": {
    "OrderApi": "https://localhost:5245"
  }
}
```

`ProxyType` can be `HTTP` or `InProcess`. `Services` maps the app name to its base URL.

### 2. Register the proxy client

```csharp
// Program.cs or Startup
var proxyType = builder.Configuration.GetValue<ProxyType>("ProxyType");
builder.Services.AddProxyClient(proxyType);
builder.Services.Configure<ServiceSettings>(builder.Configuration);
builder.Services.AddHttpClient();
builder.Services.AddHttpContextAccessor();
```

### 3. Inject `IProxy` and call the service

```csharp
app.MapGet("/place-order", async (IProxy proxy, CurrentUser user) =>
{
    // Optionally set user context (forwarded as HTTP headers)
    user.Token = "Bearer my-token";

    var orderService = proxy.Create<IOrderService>("OrderApi", "OrderService");
    var response = await orderService.PlaceOrder(new PlaceOrderRequest
    {
        CustomerId = Guid.NewGuid(),
        Items = ["item-1", "item-2"]
    });

    if (response.HasError)
        return Results.Problem(response.Error.Description);

    return Results.Ok(response.Result);
});
```

`proxy.Create<T>(appName, serviceImplName)` — `appName` matches a key in `Services` config, `serviceImplName` is the implementation class name.

### Response handling

```csharp
var res = await orderService.PlaceOrder(request);

// Check for error
if (res.HasError)
{
    Console.WriteLine($"Error {res.Error.Code}: {res.Error.Description}");
    return;
}

// Chain calls with .Next() — short-circuits on error
var cancelRes = res.Next(() => orderService.CancelOrder(new CancelOrderRequest { OrderId = res.Result!.OrderId }).Result);

// Handle specific errors
res.OnError(err =>
{
    if (err.Code == ErrorCode.NotFound) { /* ... */ }
    return err;
});
```

### InProcess proxy (same process, no HTTP)

For `ProxyType.InProcess`, also register the implementation with `AddService<T>()`:

```csharp
builder.Services.AddService<OrderService>();
```

---

## Part 3: Messaging (Publish & Subscribe via RabbitMQ)

### 1. Define an event

Events must implement `IEvent`:

```csharp
// Shared/OrderPlacedEvent.cs
public class OrderPlacedEvent : IEvent
{
    public Guid OrderId { get; set; }
    public Guid CustomerId { get; set; }
}
```

The exchange name is automatically derived from the event's full type name.

### 2. Configure messaging in appsettings.json

```json
{
  "Messaging": {
    "MessagingType": "RabbitMQ",
    "Host": "localhost",
    "Username": "guest",
    "Password": "guest"
  }
}
```

### 3. Publish an event

Register messaging and inject `IEventPublisher`:

```csharp
// Program.cs
var messagingSettings = builder.Configuration.GetSection("Messaging").Get<MessagingSettings>();
builder.Services.AddMessaging(messagingSettings!);
```

```csharp
// In a controller or service
public class OrderController : ControllerBase
{
    private readonly IEventPublisher eventPublisher;
    private readonly CurrentUser currentUser;

    public OrderController(IEventPublisher eventPublisher, CurrentUser currentUser)
    {
        this.eventPublisher = eventPublisher;
        this.currentUser = currentUser;
    }

    [HttpPost]
    public async Task<IActionResult> PlaceOrder()
    {
        // ... place order logic ...

        await eventPublisher.Publish(new OrderPlacedEvent
        {
            OrderId = Guid.NewGuid(),
            CustomerId = Guid.Parse("...")
        }, currentUser);  // currentUser metadata is forwarded in message headers

        return Ok();
    }
}
```

### 4. Subscribe to an event

The subscriber is a service method that handles the event. Register it via `RegisterSubscriber`:

```csharp
// Define the handler interface (in shared interfaces or locally)
public interface IOrderEventHandler : IService
{
    Task<Response<Empty>> Handle(OrderPlacedEvent evt);
}

// Implement it
public class OrderEventHandler : IOrderEventHandler
{
    private readonly ILogger<OrderEventHandler> logger;
    private readonly CurrentUser currentUser;  // populated from message headers

    public OrderEventHandler(ILogger<OrderEventHandler> logger, CurrentUser currentUser)
    {
        this.logger = logger;
        this.currentUser = currentUser;
    }

    public Task<Response<Empty>> Handle(OrderPlacedEvent evt)
    {
        logger.LogInformation("Order {OrderId} placed by {UserId}", evt.OrderId, currentUser.Id);
        return Task.FromResult<Response<Empty>>(new Empty());
    }
}
```

```csharp
// Program.cs — register and wire up the subscription
builder.Services.AddProxyServer();
builder.Services.AddService<OrderEventHandler>();

var messagingSettings = builder.Configuration.GetSection("Messaging").Get<MessagingSettings>();
builder.Services
    .AddMessaging(messagingSettings!)
    .RegisterSubscriber<IOrderEventHandler>(
        s => s.Handle,
        new SubscriberSettings
        {
            RetryCount = 3,          // retry on exception (default 3)
            PrefetchCount = 10,      // RabbitMQ QoS prefetch (0 = unlimited)
            UseDeadLetterQueue = false
        });
```

The subscriber runs as a hosted background service. The queue name is auto-generated as `{EventFullName}_{ServiceName}_{MethodName}`.

### Error handling in subscribers

Return an `Error` to nack and retry; return a successful `Response<Empty>` to ack:

```csharp
public Task<Response<Empty>> Handle(OrderPlacedEvent evt)
{
    if (evt.OrderId == Guid.Empty)
        return Task.FromResult<Response<Empty>>(new Error(ErrorCode.InvalidInput, "Missing OrderId"));
        // → nacked, NOT retried (only ErrorCode.Exception triggers retry)

    // success
    return Task.FromResult<Response<Empty>>(new Empty());
    // → acked
}
```

Only `ErrorCode.Exception` (thrown exceptions) triggers requeue/retry up to `RetryCount`. Business errors (`InvalidInput`, `NotFound`, etc.) are nacked without requeue.

---

## CurrentUser context

`CurrentUser` is scoped and propagated automatically:

- **HTTP proxy**: forwarded as request headers
- **Messaging**: forwarded as AMQP message headers, reconstructed on the subscriber side

```csharp
// On the caller side
currentUser.Token = "Bearer <jwt>";
currentUser.Id = userId;
currentUser.Metadata.Add(new("x-tenant-id", tenantId.ToString()));
```

---

## ErrorCode reference

| Code            | Meaning             |
| --------------- | ------------------- |
| `None`          | No error            |
| `NotFound`      | Resource not found  |
| `Exception`     | Unhandled exception |
| `Unauthorized`  | Auth failure        |
| `AlreadyExists` | Duplicate resource  |
| `InvalidInput`  | Bad request data    |
| `Skipped`       | Processing skipped  |
| `Error`         | Generic error       |
