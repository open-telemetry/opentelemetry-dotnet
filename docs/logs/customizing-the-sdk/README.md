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

A [Processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/logging-library-sdk.md#logprocessor)
is run on data between being received and exported. Generally, a processor
pre-processes data before it is exported (e.g. modify attributes or sample) or
helps ensure that data makes it through a pipeline successfully
(e.g. batch/retry).

For more information on Processors, please see [Extending the SDK](../extending-the-sdk/README.md#processor)

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
