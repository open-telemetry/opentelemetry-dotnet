# OpenTelemetry.Extensions.EventSource

This project contains an
[EventListener](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventlistener)
which can be used to translate events written to an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
into to OpenTelemetry logs.

## Usage Example

```csharp
// Step 1: Configure OpenTelemetryLoggerProvider... 
var resourceBuilder = ResourceBuilder.CreateDefault().AddService("MyService");

using var openTelemetryLoggerProvider = new OpenTelemetryLoggerProvider(options =>
{
    options
        .SetResourceBuilder(resourceBuilder)
        .AddConsoleExporter();
});

// Step 2: Create OpenTelemetryEventSourceLogEmitter to listen to events...
using var openTelemetryEventSourceLogEmitter = new OpenTelemetryEventSourceLogEmitter(
    openTelemetryLoggerProvider,
    (name) => name.StartsWith("OpenTelemetry") ? EventLevel.LogAlways : null);
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
