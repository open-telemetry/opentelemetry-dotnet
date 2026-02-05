# Sdk.CreateTracerProviderBuilder / Sdk.CreateMeterProviderBuilder

> ⚠️ **Legacy pattern (≤ 1.9.0).** These APIs still work but are no longer the
> recommended path. For new code, prefer
> [`OpenTelemetrySdk.Create()`](./opentelemetry-sdk-create.md) (non-hosted) or
> [`services.AddOpenTelemetry()`](./add-opentelemetry.md) (hosted).

## When to use

- Codebases targeting OpenTelemetry .NET SDK **< 1.10.0**.
- **Single-signal** scenarios (e.g., only tracing in a unit test).
- Quick benchmarks or throwaway scripts where minimal setup is preferred.

## Minimal example — tracing only

```csharp
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

// 1. Define an ActivitySource
var activitySource = new ActivitySource("MyApp");

// 2. Build a TracerProvider
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("MyApp")
    .ConfigureResource(r => r.AddService("my-service"))
    .AddConsoleExporter()
    .Build();

// 3. Create spans
using (var activity = activitySource.StartActivity("DoWork"))
{
    activity?.SetTag("result", "ok");
}
// TracerProvider.Dispose() is called by `using`, which flushes buffered spans.
```

## Multi-signal example

Each signal requires its own builder and its own `Dispose()` call:

```csharp
using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

// Tracing
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddSource("MyApp")
    .AddConsoleExporter()
    .Build();

// Metrics
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter("MyApp")
    .AddConsoleExporter()
    .Build();

// Logging (uses ILoggerFactory, not a provider builder)
using var loggerFactory = LoggerFactory.Create(builder => builder
    .AddOpenTelemetry(options => options.AddConsoleExporter()));

var logger = loggerFactory.CreateLogger<Program>();
logger.LogInformation("Application started");
```

> **Note:** Compare the above with
> [`OpenTelemetrySdk.Create()`](./opentelemetry-sdk-create.md), which achieves
> the same result with a single builder and a single `Dispose()`.

## API surface

### TracerProviderBuilder

| Method | Purpose |
|---|---|
| `.AddSource(params string[] names)` | Subscribe to `ActivitySource` names |
| `.AddProcessor(BaseProcessor<Activity>)` | Add a span processor |
| `.SetSampler(Sampler)` | Set the sampling strategy |
| `.ConfigureResource(Action<ResourceBuilder>)` | Attach resource attributes |
| `.AddConsoleExporter()` | Export spans to stdout |
| `.AddOtlpExporter()` | Export spans via OTLP |
| `.Build()` | Returns a `TracerProvider` |

### MeterProviderBuilder

| Method | Purpose |
|---|---|
| `.AddMeter(params string[] names)` | Subscribe to `Meter` names |
| `.AddView(...)` | Configure metric views |
| `.SetExemplarFilter(ExemplarFilterType)` | Control exemplar collection |
| `.ConfigureResource(Action<ResourceBuilder>)` | Attach resource attributes |
| `.AddConsoleExporter()` | Export metrics to stdout |
| `.AddOtlpExporter()` | Export metrics via OTLP |
| `.Build()` | Returns a `MeterProvider` |

## Lifecycle

- **Caller owns disposal.** You must call `Dispose()` (or use `using`) on each
  provider to flush pending telemetry and release resources.
- Each provider is **independent** — disposing one does not affect the others.
- In a typical app, create providers at startup and dispose at shutdown.

## Why this pattern is considered legacy

1. **Verbose for multi-signal apps.** Three builders, three disposals, three
   independent resource configurations.
2. **No shared `IServiceProvider`.** Processors and exporters that need
   dependency injection must be constructed manually.
3. **No unified lifecycle.** Each signal must be coordinated separately.

`OpenTelemetrySdk.Create()` (≥ 1.10.0) resolves all three issues while staying
host-free.
