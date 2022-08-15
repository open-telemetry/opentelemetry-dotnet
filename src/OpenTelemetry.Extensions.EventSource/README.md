# OpenTelemetry.Extensions.EventSource

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.EventSource.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.EventSource)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.EventSource.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.EventSource)

This project contains an
[EventListener](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventlistener)
which can be used to translate events written to an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
into OpenTelemetry logs.

## Installation

```shell
dotnet add package OpenTelemetry.Extensions.EventSource --prerelease
```

## Usage Example

### Configured using dependency injection

```csharp
IHost host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(builder =>
    {
        builder.ClearProviders();

        // Step 1: Configure OpenTelemetry logging...
        builder.AddOpenTelemetry(options =>
        {
            options
                .ConfigureResource(builder => builder.AddService("MyService"))
                .AddConsoleExporter()
                // Step 2: Register OpenTelemetryEventSourceLogEmitter to listen to events...
                .AddEventSourceLogEmitter((name) => name == MyEventSource.Name ? EventLevel.Informational : null);
        });
    })
    .Build();

    host.Run();
```

### Configured manually

```csharp
// Step 1: Configure OpenTelemetryLoggerProvider...
var openTelemetryLoggerProvider = Sdk.CreateLoggerProviderBuilder()
    .ConfigureResource(builder => builder.AddService("MyService"))
    .AddConsoleExporter()
    .Build();

// Step 2: Create OpenTelemetryEventSourceLogEmitter to listen to events...
using var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
    openTelemetryLoggerProvider,
    (name) => name == MyEventSource.Name ? EventLevel.Informational : null,
    disposeProvider: true);
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
