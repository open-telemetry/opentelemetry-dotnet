# services.AddOpenTelemetry()

> **This is the recommended approach for any application using the .NET Generic
> Host** (ASP.NET Core, Worker Services, MAUI, etc.).

## When to use

- **ASP.NET Core** web apps and APIs.
- **Worker services** (`BackgroundService` / `IHostedService`).
- Any application that already calls `Host.CreateDefaultBuilder()` or
  `WebApplication.CreateBuilder()`.
- Production services where you want **automatic lifecycle management** -- no
  manual `Dispose()` calls.

## Minimal example

```csharp
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("my-web-api"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());

var app = builder.Build();
app.MapGet("/", () => "Hello World");
app.Run();
```

## Full multi-signal example

```csharp
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// Clear default log providers if you want OTel to be the sole log sink
builder.Logging.ClearProviders();

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(
            serviceName: "my-web-api",
            serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddSource("MyApp")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddMeter("MyApp")
        .AddOtlpExporter())
    .WithLogging(logging => logging
        .AddOtlpExporter());

var app = builder.Build();
app.MapGet("/", () => "Hello World");
app.Run();
```

## Worker service example

```csharp
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("my-worker"))
    .WithTracing(tracing => tracing
        .AddSource("MyWorker")
        .AddOtlpExporter());

builder.Services.AddHostedService<MyWorker>();

var host = builder.Build();
host.Run();
```

## API surface

`AddOpenTelemetry()` returns an `OpenTelemetryBuilder` that implements
`IOpenTelemetryBuilder`:

| Method | Purpose |
| --- | --- |
| `.WithTracing(Action<TracerProviderBuilder>)` | Configure tracing |
| `.WithMetrics(Action<MeterProviderBuilder>)` | Configure metrics |
| `.WithLogging(Action<LoggerProviderBuilder>)` | Configure logging |
| `.ConfigureResource(Action<ResourceBuilder>)` | Set resource attributes (shared across all signals) |

## Lifecycle

- **Host-managed.** The host starts the SDK via an `IHostedService` and
  disposes it on graceful shutdown. You do **not** call `Dispose()` yourself.
- **Safe to call multiple times.** Calling `AddOpenTelemetry()` from multiple
  libraries or configuration modules is safe -- only one `TracerProvider` and
  one `MeterProvider` are created per `IServiceCollection`.
- **DI-integrated.** Processors, exporters, and any custom services registered
  in the DI container are available to the SDK. You can inject
  `TracerProvider`, `MeterProvider`, or `ILoggerFactory` anywhere via
  constructor injection.

## Dependency injection tips

### Accessing the TracerProvider

```csharp
app.MapGet("/trace-id", (TracerProvider tp) =>
{
    // TracerProvider is available via DI
    return Activity.Current?.TraceId.ToString() ?? "none";
});
```

### Registering a custom processor

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddProcessor<MyFilteringProcessor>());  // resolved from DI

builder.Services.AddSingleton<MyFilteringProcessor>();
```

## Comparison with the non-hosted builders

| Concern | `AddOpenTelemetry()` | `OpenTelemetrySdk.Create()` |
| --- | --- | --- |
| Requires Generic Host | Yes | No |
| Lifecycle | Automatic (host) | Manual (`Dispose()`) |
| DI container | App's container | SDK-internal container |
| Safe to call multiple times | Yes | N/A (single call) |
| Builder API | Same `IOpenTelemetryBuilder` | Same `IOpenTelemetryBuilder` |

> **No host?** Use
> [`OpenTelemetrySdk.Create()`](./opentelemetry-sdk-create.md) for the same
> builder API with manual lifecycle control.
