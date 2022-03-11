# Customizing OpenTelemetry .NET SDK for Logs

## OpenTelemetryLoggerProvider

As shown in the [getting-started](../getting-started/README.md) doc, a valid
`ILoggerProvider` must be configured and built to collect logs with OpenTelemetry .NET Sdk.
`OpenTelemetryLoggerProvider` holds all the configuration options.
Naturally, almost all the customizations must be done on the `OpenTelemetryLoggerProvider`.

## Building the OpenTelemetryLoggerProvider

Building the `OpenTelemteryLoggerProvider` is done either by using the static `LoggerFactory.Create()` method or the `IHostBuilder.ConfigureLogging()` method.
Either example will expose an `ILoggerBuilder` with which a user will use the `ILoggerBuilder.AddOpenTelemetry()` extension method.

The snippets below show this concept. This will create the `OpenTelemetryLoggerProvider` with the default configuration, and is not particularly useful.
The subsequent sections show how to build a more useful provider.

```csharp
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

LoggerFactory.Create(builder =>
    builder.AddOpenTelemetry(options =>
        options.AddConsoleExporter()));
```

```csharp
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

Host.CreateDefaultBuilder().ConfigureLogging(builder =>
    builder.AddOpenTelemetry(options =>
        options.AddConsoleExporter()));
```

## OpenTelemetryLoggerProvider configuration

The following concepts can be viewed in the example app [Program.cs](Program.cs)
 
 Provider holds the logging configuration, which includes the following:

1. The list of
   [Processors](https://github.com/open-telemetry/opentelemetry-collector/tree/main/processor/README.md),
   including exporting processors which exports logs to
   [Exporters](https://github.com/open-telemetry/opentelemetry-collector/blob/main/exporter/README.md)
2. The
   [Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
   associated with the logs.
3. Miscellaneous `ILogger` concepts stored in `OpenTelemetryLoggerOptions`.


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

`ILogger` implementations have a built-in mechanism to apply log filtering.
This filtering lets you define the minimum
[`LogLevel`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel)
for logs that are sent to each registered provider, including the `OpenTelemetryLoggerProvider`.
You can use the filtering either in configuration (i.e. appsettings.json) or in code.

### via appsettings.json

TODO

### via code

TODO

## Learn more

* TODO
