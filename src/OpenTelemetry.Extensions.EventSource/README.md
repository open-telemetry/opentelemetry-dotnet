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

```csharp
// Step 1: Configure OpenTelemetryLoggerProvider...
var openTelemetryLoggerProvider = new OpenTelemetryLoggerProvider(options =>
{
    options
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyService"))
        .AddConsoleExporter();
});

// Step 2: Create OpenTelemetryEventSourceLogEmitter to listen to events...
using var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
    openTelemetryLoggerProvider,
    (name) => name.StartsWith("OpenTelemetry") ? EventLevel.LogAlways : null,
    disposeProvider: true);
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
