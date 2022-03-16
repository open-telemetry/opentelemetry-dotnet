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

[Processors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/logging-library-sdk.md#logprocessor)
must be added using `OpenTelemetryLoggerOptions.AddProcessor()`. 
It is not supported to add Processors after building the `LoggerFactory`.

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.AddProcessor(...)
    });
});
```

For more information on Processors, please review [Extending the SDK](../extending-the-sdk/README.md#processor)

### SetResourceBuilder

TODO

## Filtering LogLevels

TODO

### via appsettings.json

TODO

### via code

TODO

## Learn more

* TODO
