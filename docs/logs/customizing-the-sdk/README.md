# Customizing OpenTelemetry .NET SDK for Logs

## OpenTelemetryLoggerProvider

TODO

## Building the OpenTelemetryLoggerProvider

TODO

## OpenTelemetryLoggerProvider configuration

TODO

### IncludeScopes

TODO

### IncludeFormattedMessage

TODO

### ParseStateValues

TODO

### AddProcessor

TODO

### SetResourceBuilder

A `ResourceBuilder` provides a `Resource` to the `OpenTelemetryLoggerProvider`.
A [Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
is an immutable representation of the entity producing telemetry as
[Attributes](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/common/common.md#attributes).


The snippet below demonstates creating a default `ResourceBuilder` and adding
service metadata.

```csharp
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
                serviceName: "MyService",
                serviceVersion: "1.0.0"
                ));
            options.AddConsoleExporter();
        });
});

var logger = loggerFactory.CreateLogger<Program>();

logger.LogInformation("Hello Information");
```

This example generates the following output which includes the customized
service metadata:

```
LogRecord.TraceId:            00000000000000000000000000000000
LogRecord.SpanId:             0000000000000000
LogRecord.Timestamp:          0001-01-01T00:00:00.0000000Z
LogRecord.EventId:            0
LogRecord.EventName:
LogRecord.CategoryName:       Program
LogRecord.LogLevel:           Information
LogRecord.TraceFlags:         None
LogRecord.State:              Hello Information
Resource associated with LogRecord:
    service.name: MyService
    service.version: 1.0.0
    service.instance.id: 00000000-0000-0000-0000-000000000000
```

See [Program.cs](Program.cs) for complete example.

## Filtering LogLevels

TODO

### via appsettings.json

TODO

### via code

TODO

## Learn more

* TODO
