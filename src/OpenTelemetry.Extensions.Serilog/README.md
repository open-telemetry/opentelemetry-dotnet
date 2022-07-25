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
var openTelemetryLoggerProvider = new OpenTelemetryLoggerProvider(options =>
{
    options
        .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyService"))
        .AddConsoleExporter();
});

// Step 2: Register OpenTelemetry sink with Serilog...
Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(openTelemetryLoggerProvider, disposeProvider: true)
    .CreateLogger();

// Step 3: When application is shutdown flush all log messages and dispose provider...
Log.CloseAndFlush();
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
