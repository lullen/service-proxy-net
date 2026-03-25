# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

```bash
# Build
dotnet build

# Restore dependencies
dotnet restore

# Run the sample server
dotnet run --project HttpServer

# Run the sample client
dotnet run --project HttpClient

# Run the Aspire orchestration (starts all services)
dotnet run --project Luizio.AppHost

# Pack the NuGet package
dotnet pack Luizio.iFX/Luizio.iFX.csproj
```

There are no automated tests in this repo — the `Test/` project is a manual console app for integration testing.

## Architecture

**Luizio.iFX** is a .NET service proxy framework enabling transparent remote service invocation via HTTP, InProcess, or Dapr transport. It is packaged as a NuGet library consumed by other services.

### Core Idea

Services are defined as interfaces (in `Server.Interfaces/`) and implemented in `Server/`. Client projects consume these interfaces via a generated proxy — no direct references to the implementation. The proxy intercepts calls and dispatches them over the configured transport.

**Method signature requirement:** All proxied methods must return `Task<Response<T>>`.

### Request Flow

1. Client calls a method on a proxied interface
2. `DispatchProxy.Invoke` intercepts the call in `ServiceProxy<T>` (`Luizio.iFX/Client/Proxy.cs`)
3. Delegates to the active transport: `HttpServiceProxy`, `InProcServiceProxy`, or `DaprServiceProxy`
4. HTTP transport POSTs to `/{ServiceName}/{MethodName}` on the target service
5. Server-side: `app.MapService<TImpl>()` registers these routes and deserializes requests
6. Response is deserialized back into `Response<T>` on the client

### Key Components

**`Luizio.iFX/Models/Response.cs`** — Generic response wrapper. Supports fluent chaining via `.Next()` and `.OnError()`. Errors carry an `ErrorCode` enum value.

**`Luizio.iFX/Models/CurrentUser.cs`** — User context propagated across service boundaries via HTTP headers. Contains Bearer token and custom metadata. Populated by `CurrentUserMiddleware`.

**`Luizio.iFX/Client/`** — Client-side proxy factory and transport implementations. `ProxyClientExtension.cs` registers everything via `AddProxyClient()`.

**`Luizio.iFX/Server/`** — Server-side extensions. `AddProxyServer()` + `MapService<TImpl>()` expose a service implementation over HTTP.

**`Luizio.iFX/Messaging/`** — RabbitMQ-based event publishing/subscribing via `IEventPublisher` / `IEventSubscriber`.

### Configuration

Services are discovered via `appsettings.json`:
```json
{
  "ProxyType": "HTTP",
  "Services": {
    "HttpServer": "https://localhost:5245"
  }
}
```

`ProxyType` selects the transport: `HTTP`, `InProcess`, or `Dapr`.

### Projects

| Project | Role |
|---|---|
| `Luizio.iFX` | Core framework library (the published NuGet package) |
| `Luizio.iFX.Testing` | Test helpers for projects using the framework |
| `Server.Interfaces` | Shared service interface definitions |
| `Server` | Service implementations |
| `HttpServer` | ASP.NET Core host exposing `Server` via HTTP |
| `HttpClient` | Console app consuming services through the proxy |
| `Luizio.AppHost` | .NET Aspire orchestration host |
| `Luizio.ServiceDefaults` | Shared Aspire service configuration (OpenTelemetry, resilience) |
| `MessageTest` | RabbitMQ event publishing/subscribing demo |
| `Test` | Manual integration test console app |

### Publish / Deploy

The deploy workflow (`.github/workflows/deploy.yml`) is triggered manually and publishes the NuGet package. The build workflow (`.github/workflows/build.yml`) runs on every push to `master`.
