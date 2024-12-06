# OpenTelemetry .NET SDK

## Initialize the SDK

There are two different common initialization styles supported by OpenTelemetry.

### Initialize the SDK using a host

Users building applications based on
[Microsoft.Extensions.Hosting](https://www.nuget.org/packages/Microsoft.Extensions.Hosting)
should utilize the
[OpenTelemetry.Extensions.Hosting](../src/OpenTelemetry.Extensions.Hosting/README.md)
package to initialize OpenTelemetry. This style provides a deep integration
between the host infrastructure (`IServiceCollection`, `IServiceProvider`,
`IConfiguration`, etc.) and OpenTelemetry.

[AspNetCore](https://learn.microsoft.com/aspnet/core/fundamentals/host/web-host)
applications are the most common to use the hosting model but there is also a
[Generic Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host)
which may be used in console, service, and worker applications.

> [!NOTE]
> When using `OpenTelemetry.Extensions.Hosting` only a single pipeline will be
> created for each configured signal (logging, metrics, and/or tracing). Users
> who need more granular control can create additional pipelines using the
> manual style below.

First install the
[OpenTelemetry.Extensions.Hosting](../src/OpenTelemetry.Extensions.Hosting/README.md)
package.

Second call the `AddOpenTelemetry` extension using the host
`IServiceCollection`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// Clear the default logging providers added by the host
builder.Logging.ClearProviders();

// Initialize OpenTelemetry
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => /* Resource configuration goes here */)
    .WithLogging(logging => /* Logging configuration goes here */)
    .WithMetrics(metrics => /* Metrics configuration goes here */)
    .WithTracing(tracing => /* Tracing configuration goes here */));
```

> [!NOTE]
> Calling `WithLogging` automatically registers the OpenTelemetry
> `ILoggerProvider` and enables `ILogger` integration.

### Initialize the SDK manually

Users running on .NET Framework or running without a host may initialize
OpenTelemetry manually.

> [!IMPORTANT]
> When initializing OpenTelemetry manually make sure to ALWAYS dispose the SDK
> and/or providers when the application is shutting down. Disposing
> OpenTelemetry gives the SDK a chance to flush any telemetry held in memory.
> Skipping this step may result in data loss.

First install the [OpenTelemetry SDK](../src/OpenTelemetry/README.md) package or
an exporter package such as
[OpenTelemetry.Exporter.OpenTelemetryProtocol](../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md).
Exporter packages typically reference the SDK and will make it available via
transitive reference.

Second use one of the following initialization APIs (depending on the SDK
version being used):

#### Using 1.10.0 or newer

The `OpenTelemetrySdk.Create` API can be used to initialize all signals off a
single root builder and supports cross-cutting extensions such as
`ConfigureResource` which configures a `Resource` to be used by all enabled
signals. An `OpenTelemetrySdk` instance is returned which may be used to access
providers for each signal. Calling `Dispose` on the returned instance will
gracefully shutdown the SDK and flush any telemetry held in memory.

> [!NOTE]
> When calling `OpenTelemetrySdk.Create` a dedicated `IServiceCollection` and
> `IServiceProvider` will be created for the SDK and shared by all signals. An
> `IConfiguration` is created automatically from environment variables.

```csharp
using OpenTelemetry;

var sdk = OpenTelemetrySdk.Create(builder => builder
    .ConfigureResource(resource => /* Resource configuration goes here */)
    .WithLogging(logging => /* Logging configuration goes here */)
    .WithMetrics(metrics => /* Metrics configuration goes here */)
    .WithTracing(tracing => /* Tracing configuration goes here */));

// Optionally ForceFlush() telemetry objects in memory
sdk.LoggerProvider.ForceFlush();
sdk.MeterProvider.ForceFlush();
sdk.TracerProvider.ForceFlush();

// During application shutdown
sdk.Dispose();
```

To obtain an `ILogger` instance for emitting logs when using the
`OpenTelemetrySdk.Create` API call the `GetLoggerFactory` extension method using
the returned `OpenTelemetrySdk` instance:

```csharp
var logger = sdk.GetLoggerFactory().CreateLogger<Program>();
logger.LogInformation("Application started");
```

#### Using 1.9.0 or older

The following shows how to create providers for each individual signal. Each
provider is independent and must be managed and disposed explicitly. There is no
mechanism using this style to perform cross-cutting actions across signals.

```csharp
using Microsoft.Extensions.Logging;
using OpenTelemetry;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    /* Tracing configuration goes here */
    .Build();

var meterProvider = Sdk.CreateMeterProviderBuilder()
    /* Metrics configuration goes here */
    .Build();

var loggerFactory = LoggerFactory.Create(builder => builder
    .AddOpenTelemetry(options => /* Logging configuration goes here */));

// During application shutdown
tracerProvider.Dispose();
meterProvider.Dispose();
loggerFactory.Dispose();
```
