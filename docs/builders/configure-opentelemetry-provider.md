# ConfigureOpenTelemetry*Provider Extensions

These `IServiceCollection` extension methods register actions used to
configure OpenTelemetry providers. They work anywhere you have access to an
`IServiceCollection` - in application startup, inside a builder's
`ConfigureServices` callback, or in a shared extension method.

## When to use

- You want to **register sources, meters, or logging configuration separately**
  from where the provider is created (e.g., different files, methods, or
  packages).
- You need to **add configuration from inside a `ConfigureServices` callback**
  on a provider builder.
- Your app has **modular startup** where each feature area configures its own
  instrumentation independently.
- You are a **library author** registering instrumentation without owning the
  provider creation call.
- You need access to `IServiceProvider` at configuration time to resolve
  services such as `IConfiguration`.

> [!IMPORTANT]
> These methods register configuration but **do not create a
> provider**. A provider must still be created via
> [Host & DI-Integrated (`AddOpenTelemetry`)][add],
> [Unified Multi-Signal (`OpenTelemetrySdk.Create`)][create], or
> [Per-Signal / Legacy (`Sdk.CreateTracerProviderBuilder`)][tracer]
> for the configuration to take effect.

## Namespaces

| Method | Namespace |
| --- | --- |
| `ConfigureOpenTelemetryTracerProvider` | `OpenTelemetry.Trace` |
| `ConfigureOpenTelemetryMeterProvider` | `OpenTelemetry.Metrics` |
| `ConfigureOpenTelemetryLoggerProvider` | `OpenTelemetry.Logs` |

## Available methods

| Method | Signal |
| --- | --- |
| `services.ConfigureOpenTelemetryTracerProvider(...)` | Tracing |
| `services.ConfigureOpenTelemetryMeterProvider(...)` | Metrics |
| `services.ConfigureOpenTelemetryLoggerProvider(...)` | Logging |

### Overload 1 - `Action<*ProviderBuilder>` (executed immediately)

```csharp
public static IServiceCollection ConfigureOpenTelemetryTracerProvider(
    this IServiceCollection services,
    Action<TracerProviderBuilder> configure);

public static IServiceCollection ConfigureOpenTelemetryMeterProvider(
    this IServiceCollection services,
    Action<MeterProviderBuilder> configure);

public static IServiceCollection ConfigureOpenTelemetryLoggerProvider(
    this IServiceCollection services,
    Action<LoggerProviderBuilder> configure);
```

The `configure` delegate is **invoked immediately** (not deferred). Internally
it creates a `*ProviderServiceCollectionBuilder` (e.g.,
`TracerProviderServiceCollectionBuilder`) wrapping the `IServiceCollection` and
passes it to the delegate. Because the delegate receives the builder before the
`IServiceProvider` exists, it is **safe to register services** (e.g., call
`AddSource`, `AddMeter`, `AddInstrumentation<T>`, etc.).

### Overload 2 - `Action<IServiceProvider, *ProviderBuilder>` (deferred)

```csharp
public static IServiceCollection ConfigureOpenTelemetryTracerProvider(
    this IServiceCollection services,
    Action<IServiceProvider, TracerProviderBuilder> configure);

public static IServiceCollection ConfigureOpenTelemetryMeterProvider(
    this IServiceCollection services,
    Action<IServiceProvider, MeterProviderBuilder> configure);

public static IServiceCollection ConfigureOpenTelemetryLoggerProvider(
    this IServiceCollection services,
    Action<IServiceProvider, LoggerProviderBuilder> configure);
```

This overload registers the delegate as an `IConfigureTracerProviderBuilder` /
`IConfigureMeterProviderBuilder` / `IConfigureLoggerProviderBuilder` singleton
in the `IServiceCollection`. The delegate is **not invoked until the provider
is being constructed** (when the `IServiceProvider` is available). Because the
service container is already built at that point, **you cannot register new
services** inside this callback - many builder helper extensions that register
services will throw `NotSupportedException`.

## Example - separating source registration from exporter setup

Register instrumentation sources early in startup, then configure exporters
separately:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Register sources (could be in a different file or method)
builder.Services.ConfigureOpenTelemetryTracerProvider(tracing =>
    tracing.AddSource("MyApp"));

builder.Services.ConfigureOpenTelemetryMeterProvider(metrics =>
    metrics.AddMeter("MyApp"));

// Configure exporters and create the provider
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService("my-app"))
    .WithTracing(tracing => tracing.AddOtlpExporter())
    .WithMetrics(metrics => metrics.AddOtlpExporter());

var app = builder.Build();
app.Run();
```

## Example - library author registering sources

A library can register its instrumentation without depending on the app's
startup code:

```csharp
public static class MyLibraryExtensions
{
    public static IServiceCollection AddMyLibrary(this IServiceCollection services)
    {
        services.AddSingleton<MyService>();

        services.ConfigureOpenTelemetryTracerProvider(tracing =>
            tracing.AddSource("MyLibrary"));
        services.ConfigureOpenTelemetryMeterProvider(metrics =>
            metrics.AddMeter("MyLibrary"));

        return services;
    }
}
```

The host application composes everything:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMyLibrary();  // library registers its sources

builder.Services.AddOpenTelemetry()          // app owns the provider
    .ConfigureResource(r => r.AddService("my-app"))
    .WithTracing(tracing => tracing.AddOtlpExporter())
    .WithMetrics(metrics => metrics.AddOtlpExporter());

var app = builder.Build();
app.Run();
```

## Example - with `Sdk.CreateTracerProviderBuilder()` (non-hosted)

These methods also work with the per-signal builders via `ConfigureServices`:

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureServices(services =>
    {
        services.ConfigureOpenTelemetryTracerProvider(
            tracing => tracing.AddSource("MyApp"));

        services.ConfigureOpenTelemetryTracerProvider(
            (sp, tracing) =>
            {
                // Resolve services - but cannot register new ones here
                var config = sp.GetRequiredService<IConfiguration>();
                tracing.AddInstrumentation(
                    sp.GetRequiredService<MyInstrumentation>());
            });
    })
    .AddConsoleExporter()
    .Build();
```

## Example - modular registration

Each feature module configures its own sources independently:

```csharp
// OrdersModule.cs
public static IServiceCollection AddOrders(this IServiceCollection services)
{
    services.ConfigureOpenTelemetryTracerProvider(tracing =>
        tracing.AddSource("Orders"));
    services.ConfigureOpenTelemetryMeterProvider(metrics =>
        metrics.AddMeter("Orders"));
    return services;
}

// PaymentsModule.cs
public static IServiceCollection AddPayments(this IServiceCollection services)
{
    services.ConfigureOpenTelemetryTracerProvider(tracing =>
        tracing.AddSource("Payments"));
    services.ConfigureOpenTelemetryMeterProvider(metrics =>
        metrics.AddMeter("Payments"));
    return services;
}

// Program.cs
builder.Services.AddOrders();
builder.Services.AddPayments();
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddOtlpExporter())
    .WithMetrics(metrics => metrics.AddOtlpExporter());
```

## Example - IServiceProvider overload

Use the `Action<IServiceProvider, *ProviderBuilder>` overload when you need to
resolve services during configuration:

```csharp
services.ConfigureOpenTelemetryTracerProvider((sp, tracing) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var samplingRate = config.GetValue<double>("Telemetry:SamplingRate");

    tracing.SetSampler(new TraceIdRatioBasedSampler(samplingRate));
});
```

> [!WARNING]
> You cannot register new services inside this callback -
> the `IServiceProvider` is already built at this point. Adding services
> (many helper extensions do this) will throw `NotSupportedException`. If you
> don't need `IServiceProvider` access, use the simpler overload which is
> safe to use with helper extensions.

## Relationship with `WithTracing` / `WithMetrics` / `WithLogging`

The `ConfigureOpenTelemetry*Provider` methods and the `With*` methods on
`IOpenTelemetryBuilder` are **complementary**, not interchangeable:

- `WithTracing()` / `WithMetrics()` / `WithLogging()` **register the
  provider** in the `IServiceCollection` (e.g., as a `TracerProvider`
  singleton, or by registering an `IMetricsListener` / `ILoggerProvider`).
  Without calling one of these, no provider is created and the queued
  configuration actions are never consumed.
- `ConfigureOpenTelemetry*Provider` only **queues configuration actions** --
  it does not register a provider.

Internally, `WithTracing` creates a `TracerProviderBuilderBase` (which
creates a `TracerProviderServiceCollectionBuilder`, which itself calls
`ConfigureOpenTelemetryTracerProvider` to queue its own actions). The SDK's
`ConfigureResource` method on `IOpenTelemetryBuilder` also uses
`ConfigureOpenTelemetryTracerProvider`, `ConfigureOpenTelemetryMeterProvider`,
and `ConfigureOpenTelemetryLoggerProvider` internally to distribute the
resource configuration to each signal.

## Can these be used without the hosting package?

**Yes.** These methods are defined in
`OpenTelemetry.Api.ProviderBuilderExtensions`, not in
`OpenTelemetry.Extensions.Hosting`. They work in any scenario that uses an
`IServiceCollection`:

- With `AddOpenTelemetry()` from `OpenTelemetry.Extensions.Hosting` (hosted
  apps).
- With `OpenTelemetrySdk.Create()` (non-hosted apps) - the `IServiceCollection`
  is created internally, and the `IOpenTelemetryBuilder.Services` property
  exposes it.
- With `Sdk.CreateTracerProviderBuilder()` /
  `Sdk.CreateMeterProviderBuilder()` / `Sdk.CreateLoggerProviderBuilder()` -
  accessible via `.ConfigureServices(services => ...)`.
- In any custom code that creates its own `ServiceCollection` and resolves a
  provider from it.

The key requirement is that a provider must eventually be **resolved** from
the same `IServiceCollection` for the queued actions to take effect.

## Related methods

| Method | Defined on | Purpose |
| --- | --- | --- |
| [Host & DI-Integrated (`AddOpenTelemetry`)][add] | `IServiceCollection` | Creates the provider and starts the hosted service |
| `.WithTracing()` / `.WithMetrics()` / `.WithLogging()` | `IOpenTelemetryBuilder` | Registers a provider for the signal and optionally configures it |
| `.ConfigureResource()` | `IOpenTelemetryBuilder` | Shared resource configuration (internally uses `ConfigureOpenTelemetry*Provider`) |

> These methods work with **any `IServiceCollection`** - hosted or not. For
> non-hosted apps, use them with
> [`OpenTelemetrySdk.Create()`](./opentelemetry-sdk-create.md) via
> `builder.Services`, or with the per-signal
> [`Sdk.CreateTracerProviderBuilder()`](./sdk-create-tracer-provider-builder.md)
> via `.ConfigureServices(services => ...)`.

[add]: ./add-opentelemetry.md
[create]: ./opentelemetry-sdk-create.md
[tracer]: ./sdk-create-tracer-provider-builder.md
