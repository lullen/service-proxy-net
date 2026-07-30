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

The exchange name is automatically derived from the event's full type name, and is declared on first
publish (and by each subscriber for the exchanges it binds). `IEvent` is enforced by the compiler on
`Publish<T>` and at registration for `bindTo` entries — a type that isn't an `IEvent` can never be
published, so subscribing to one could only ever wait forever.

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

### 5. One handler for many event types

When several event types share a shape, the handler's parameter can be an interface (or abstract
base) they all implement. Pass `bindTo` listing the concrete types to bind:

```csharp
public interface IOrderTransitionEvent : IEvent
{
    Guid OrderId { get; set; }
    DateTime Timestamp { get; set; }
}

// One handler for all of them
public Task<Response<Empty>> OnTransition(IOrderTransitionEvent evt)
{
    logger.LogInformation("Order {OrderId} moved at {Timestamp}", evt.OrderId, evt.Timestamp);

    if (evt is OrderPlacedEvent placed)   // pattern-match for type-specific fields
        logger.LogInformation("Placed by {CustomerId}", placed.CustomerId);

    return Task.FromResult<Response<Empty>>(new Empty());
}
```

```csharp
    .RegisterSubscriber<IOrderEventHandler>(
        s => s.OnTransition,
        new SubscriberSettings { RetryCount = 3, UseDeadLetterQueue = true },
        bindTo: [typeof(OrderPlacedEvent), typeof(OrderShippedEvent), typeof(OrderCancelledEvent)]);
```

This creates **one** queue, named after the parameter type
(`…IOrderTransitionEvent_orderevenhandler_ontransition`), bound to one exchange per `bindTo` entry.
The handler receives the real concrete event with every field intact.

Publishing does not change — publishers keep calling `Publish(new OrderPlacedEvent { … })`, and
existing concrete-type subscriptions are unaffected.

Notes:

- `bindTo` is **declared, not discovered**. An assembly scan sees only loaded assemblies and could
  silently miss an event type; adding a new event type means implementing the interface *and* adding
  it to `bindTo`.
- It is also the allowlist used to resolve each delivery's type, so a publisher cannot make the
  subscriber instantiate an arbitrary type.
- Registration throws at startup if `bindTo` is empty, contains an abstract type, contains a
  duplicate, contains a non-`IEvent`, or contains anything not assignable to the handler's parameter.
  It also throws if two subscriptions would share a queue name (same service and method), since that
  makes them competing consumers and each message would reach only one.
- Omit `bindTo` for a single concrete event type — that is the default and behaves exactly as before.
  A handler taking an interface *without* `bindTo` throws at registration.
- Narrowing a `bindTo` list leaves stale bindings on the existing queue; remove them, or deliveries
  from the dropped exchange are rejected to the DLQ.

### Error handling in subscribers

Return an `Error` to fail the message; return a successful `Response<Empty>` to ack:

```csharp
public Task<Response<Empty>> Handle(OrderPlacedEvent evt)
{
    if (evt.OrderId == Guid.Empty)
        return Task.FromResult<Response<Empty>>(new Error(ErrorCode.InvalidInput, "Missing OrderId"));
        // → dead-lettered, NOT retried (only ErrorCode.Exception triggers retry)

    // success
    return Task.FromResult<Response<Empty>>(new Empty());
    // → acked
}
```

Only `ErrorCode.Exception` (thrown exceptions) triggers retry, up to `RetryCount`. A retry is
redelivered to **that subscriber's queue alone**, never republished to the fanout exchange. Business
errors (`InvalidInput`, `NotFound`, etc.) are not retried — the same input would reach the same
conclusion — and go straight to the dead-letter queue.

Once the retry budget is exhausted, or for any error that can never succeed (a payload that will not
deserialize, a delivery from an exchange not in `bindTo`), the message is rejected without requeue.

`UseDeadLetterQueue = true` declares `{EventFullName}_dlq` and configures the queue to dead-letter
into it. **Without it, a failed message is discarded.** Note that RabbitMQ cannot change the
arguments of an existing queue: turning the flag on for a subscriber that has already run requires
deleting and redeclaring its queue, which must be scheduled when that queue is drained.

Retries have **no backoff** — the redelivery is immediate, so a failing message spends its whole
budget in milliseconds. That helps for a transient blip, not for a dependency that is down.

### Messaging telemetry

`AddMessaging` registers the source and meter with OpenTelemetry itself, so no extra wiring is
needed. Nothing is exported unless the host configures an OTLP exporter.

**Traces.** Publishing emits a `publish {EventFullName}` span (`ActivityKind.Producer`) and injects
W3C trace context into the message headers. Consuming emits a `process {QueueName}` span
(`ActivityKind.Consumer`).

The consume span is deliberately **a new trace, not a continuation of the publish**, with an
`ActivityLink` back to the producer's context. Consuming runs on its own schedule — a queue can be
backed up for minutes, and a retry happens later still — so grafting it onto the publisher's trace
would produce spans that appear to last for the whole queue delay. In Grafana/Tempo the two traces
are separate and reachable from each other via the link.

Because the consume span makes `Activity.Current` non-null, the handler invocation now also gets its
nested `Proxy` span. Previously the message path produced no spans at all, since
`InProcServiceProxy` only starts one when there is already an ambient activity.

Attributes follow OTel messaging conventions (`messaging.system`, `messaging.operation.type`,
`messaging.destination.name`, `messaging.destination.subscription.name`, `messaging.message.type`),
plus `messaging.ifx.outcome` and `error.type` on failure.

A `TraceId` property on the event payload does **not** participate in any of this — it is ordinary
data, invisible to the collector.

**Metrics** (meter `Luizio.iFX.Messaging`):

| Instrument | Tags | Use |
| ---------- | ---- | --- |
| `messaging_events_published` | `exchange` | Publish rate |
| `messaging_events_consumed` | `queue`, `event_type`, `outcome` | Consume rate and health |

`outcome` is one of `processed`, `retried`, `dead_lettered`, `discarded`. **Alert on
`dead_lettered` and `discarded`** — both mean a message was dropped from the business flow with
nothing but a log line. An unresolvable event type is tagged `event_type=unknown` rather than the
name from the wire, so a publisher cannot drive unbounded metric cardinality.

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
