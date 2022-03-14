# Customizing OpenTelemetry .NET SDK for Logs

## OpenTelemetryLoggerProvider

As shown in the [getting-started](../getting-started/README.md) doc,
calling the extension method `ILoggerBuilder.AddOpenTelemetry` will register
`OpenTelemetryLoggerProvider` as an `ILoggerProvider` using the default
configuration. By itself, this is not particularly useful. The subsequent
sections show how to build a more useful provider.

## OpenTelemetryLoggerProvider configuration

The following concepts can be viewed in the example app
[Program.cs](Program.cs)

`OpenTelemetryLoggerProvider` holds the logging configuration
`OpenTelemetryLoggerOptions`, which includes the following:

1. The list of
   [Processors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/logging-library-sdk.md#logprocessor),
   including exporting processors which pass log records to
   [Exporters](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/logging-library-sdk.md#logexporter)
2. The
   [Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
   associated with the logs.
3. Miscellaneous ILogger concepts.

### IncludeScopes

TODO

### IncludeFormattedMessage

TODO

### ParseStateValues

TODO

### AddProcessor

TODO

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
