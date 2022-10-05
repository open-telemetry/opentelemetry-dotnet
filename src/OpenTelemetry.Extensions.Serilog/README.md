# OpenTelemetry.Extensions.Serilog

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Serilog.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Serilog)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Serilog.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Serilog)

This project contains a [Serilog](https://github.com/serilog/)
[sink](https://github.com/serilog/serilog/wiki/Configuration-Basics#sinks) for
writing log messages to OpenTelemetry and an
[enricher](https://github.com/serilog/serilog/wiki/Configuration-Basics#enrichers)
for adding OpenTelemetry trace details to log messages.

## Installation

```shell
dotnet add package OpenTelemetry.Extensions.Serilog --prerelease
```

## Usage Examples

### Sink

Use the sink when you want to capture & translate Serilog log messages into
OpenTelemetry. This is known as a "log appender" in the OpenTelemetry
Specification. Log messages will flow through the OpenTelemetry pipeline to any
registered processors/exporters.

```csharp
// Step 1: Configure OpenTelemetryLoggerProvider...
var loggerProvider = Sdk.CreateLoggerProviderBuilder()
    .ConfigureResource(builder => builder.AddService("MyService"))
    .AddConsoleExporter()
    .Build();

// Step 2: Register OpenTelemetry sink with Serilog...
Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(openTelemetryLoggerProvider, disposeProvider: true) // <-- Register sink
    .CreateLogger();

// Step 3: When application is shutdown flush all log messages and dispose provider...
Log.CloseAndFlush();
```

### Enricher

Use the enricher when you want to add trace details to Serilog log messages.

Note: It is not necessary to use the enricher when using the sink above. The
enricher is provider for log messages that are NOT flowing through OpenTelemetry
for example when logging to JSON files. When using the sink trace details are
included automatically.

```csharp
// Step 1: Register OpenTelemetry enricher with Serilog...
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .Enrich.WithOpenTelemetry() // <-- Register enricher
    .CreateLogger();

// Step 2: Start a trace...
using var myActivity = myActivitySource.StartActivity(
    activityKind,
    startTime: DateTimeOffset.NowUtc,
    name: name);

// Step 3: Generate log messages through Serilog...
Log.Logger.Information("Starting application");
```

The example above will output this JSON:

```json
{
    "Timestamp": "2022-09-26T02:45:07.1008180-04:00",
    "Level": "Information",
    "MessageTemplate": "Application starting",
    "RenderedMessage": "Application starting",
    "Properties": {
        "SpanId": "9250f033e82cc807",
        "TraceId": "a1c08f86409507de8bf6e38416c8f3de",
        "TraceFlags": "None"
    }
}
```

Note: In cases where you have a nested activity the property `ParentSpanId` will
also be included.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
