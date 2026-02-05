# OpenTelemetrySdk.Create()

> **Introduced in 1.10.0.** This is the recommended way to initialize the SDK
> in non-hosted applications.

## When to use

- **Console apps, CLI tools, background jobs** that do not use the .NET Generic
  Host.
- Any non-hosted process that needs **multiple signals** (traces + metrics +
  logs) under a single lifecycle boundary.

## Minimal example

```csharp
using OpenTelemetry;

using var sdk = OpenTelemetrySdk.Create(builder => builder
    .ConfigureResource(r => r.AddService("my-console-app"))
    .WithTracing(tracing => tracing
        .AddSource("MyApp")
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("MyApp")
        .AddConsoleExporter()));

// Application logic goes here.
// sdk.Dispose() is called by `using`, which flushes all signals.
```

## Using logging

`OpenTelemetrySdk` exposes an `ILoggerFactory` via the `GetLoggerFactory()`
extension method:

```csharp
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

using var sdk = OpenTelemetrySdk.Create(builder => builder
    .WithLogging(logging => logging.AddConsoleExporter()));

var logger = sdk.GetLoggerFactory().CreateLogger<Program>();
logger.LogInformation("Hello from {Source}", "OpenTelemetry");
```

## Full multi-signal example

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

var activitySource = new ActivitySource("MyApp");
var meter = new Meter("MyApp");
var requestCounter = meter.CreateCounter<long>("requests");

using var sdk = OpenTelemetrySdk.Create(builder => builder
    .ConfigureResource(r => r.AddService("my-console-app"))
    .WithTracing(tracing => tracing
        .AddSource("MyApp")
        .AddOtlpExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("MyApp")
        .AddOtlpExporter())
    .WithLogging(logging => logging
        .AddOtlpExporter()));

var logger = sdk.GetLoggerFactory().CreateLogger<Program>();

using (var activity = activitySource.StartActivity("ProcessRequest"))
{
    requestCounter.Add(1);
    logger.LogInformation("Request processed");
}
// sdk.Dispose() flushes and shuts down all three providers.
```

## API surface

`OpenTelemetrySdk.Create()` accepts an `Action<IOpenTelemetryBuilder>`. The
builder exposes the same extension methods used by the hosted
`AddOpenTelemetry()` path:

| Method | Purpose |
|---|---|
| `.WithTracing(Action<TracerProviderBuilder>)` | Configure tracing |
| `.WithMetrics(Action<MeterProviderBuilder>)` | Configure metrics |
| `.WithLogging(Action<LoggerProviderBuilder>)` | Configure logging |
| `.ConfigureResource(Action<ResourceBuilder>)` | Set resource attributes (shared across all signals) |

## Returned object

```
OpenTelemetrySdk : IDisposable
├── .TracerProvider   → TracerProvider
├── .MeterProvider    → MeterProvider
├── .LoggerProvider   → LoggerProvider
└── .GetLoggerFactory() → ILoggerFactory   (extension method)
```

Unconfigured signals return **no-op** provider instances — safe to access but
they emit nothing.

## Lifecycle

- **Single disposal.** Calling `sdk.Dispose()` shuts down the internal
  `IServiceProvider`, which gracefully flushes and disposes all configured
  providers.
- **Internally creates its own DI container.** Processors, exporters, and
  other services registered via `.ConfigureServices()` live inside this
  container.
- **Environment-variable support.** An `IConfiguration` instance backed by
  environment variables is created automatically, so standard `OTEL_*`
  environment variables are respected.

## Comparison with the per-signal builders

| Concern | `OpenTelemetrySdk.Create()` | `Sdk.CreateTracerProviderBuilder()` |
|---|---|---|
| Signals | All in one builder | One builder per signal |
| Resource config | Shared `.ConfigureResource()` | Must repeat per builder |
| Disposal | Single `Dispose()` | One `Dispose()` per provider |
| Internal DI | ✅ | ❌ |
| Minimum SDK version | 1.10.0 | Any |

> **Need the .NET Host?** Use
> [`services.AddOpenTelemetry()`](./add-opentelemetry.md) instead — same
> builder API, but lifecycle is managed by the host.
