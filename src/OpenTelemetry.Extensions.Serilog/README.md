# OpenTelemetry.Extensions.Serilog

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Serilog.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Serilog)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Serilog.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Serilog)

This project contains a [Serilog](https://github.com/serilog/)
[sink](https://github.com/serilog/serilog/wiki/Configuration-Basics#sinks) for
writing log messages to OpenTelemetry.

## Installation

```shell
dotnet add package OpenTelemetry.Extensions.Serilog --prerelease
```

## Usage Example

```csharp
// Step 1: Configure OpenTelemetryLoggerProvider...
var openTelemetryLoggerProvider = Sdk.CreateLoggerProviderBuilder()
    .ConfigureResource(builder => builder.AddService("MyService"))
    .AddConsoleExporter()
    .Build();

// Step 2: Register OpenTelemetry sink with Serilog...
Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(openTelemetryLoggerProvider, disposeProvider: true)
    .CreateLogger();

// Step 3: When application is shutdown flush all log messages and dispose provider...
Log.CloseAndFlush();
```

## Activity Enricher
The next example assumes that at least one `ActivityListener` have been registered prior to calling `StartActivity`,
otherwise, `StartActivity` will return `null`.
```csharp
Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(openTelemetryLoggerProvider, disposeProvider: true)
    .Enrich.WithOpenTelemetry() // <-- Register enricher
    .CreateLogger();

/// ...
ActivitySource activitySource = new(ServiceName);
activitySource.StartActivity(
    activityKind,
    startTime: DateTimeOffset.NowUtc,
    name: name
);

Log.Logger.Information("Starting application");
/// ...
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

In cases where you have a nested activity, the property `ParentSpanId` will be included.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
