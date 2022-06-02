# OpenTelemetry.Extensions.Serilog

This project contains a [Serilog](https://github.com/serilog/)
[sink](https://github.com/serilog/serilog/wiki/Configuration-Basics#sinks) for
writing log messages to OpenTelemetry.

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

// Step 2: Register OpenTelemetry sink with Serilog...
Log.Logger = new LoggerConfiguration()
    .WriteTo.OpenTelemetry(openTelemetryLoggerProvider)
    .CreateLogger();
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
