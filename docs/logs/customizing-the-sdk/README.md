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

[Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
is the immutable representation of the entity producing the telemetry.
If no `Resource` is explicitly configured, the default is to use a resource
indicating this [Telemetry
SDK](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions#telemetry-sdk).
The `SetResourceBuilder` method can be used to set a single `ResourceBuilder`.
If `SetResourceBuilder` is called multiple times, only the last is kept..
It is not possible to change the resource builder *after* creating the
LoggerFactory.

The snippet below shows configuring a custom `ResourceBuilder` to the provider.

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

This example generates the following output which includes the Resource
information:

```text
LogRecord.TraceId:            00000000000000000000000000000000
LogRecord.SpanId:             0000000000000000
LogRecord.Timestamp:          1970-01-01T00:00:01.0000000Z
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

It is also possible to configure the `Resource` by using following
environmental variables:

<!-- markdownlint-disable MD013 -->
| Environment variable       | Description                                        |
| -------------------------- | -------------------------------------------------- |
| `OTEL_RESOURCE_ATTRIBUTES` | Key-value pairs to be used as resource attributes. See the [Resource SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable) for more details. |
| `OTEL_SERVICE_NAME`        | Sets the value of the `service.name` resource attribute. If `service.name` is also provided in `OTEL_RESOURCE_ATTRIBUTES`, then `OTEL_SERVICE_NAME` takes precedence. |
<!-- markdownlint-enable MD013 -->

## Filtering LogLevels

TODO

### via appsettings.json

TODO

### via code

TODO

## Learn more

* TODO
