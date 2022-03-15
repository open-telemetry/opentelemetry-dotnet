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

TODO

## Filtering LogLevels

[`ILogger`](https://docs.microsoft.com/dotnet/core/extensions/logging)
implementations have a built-in mechanism to apply [log
filtering](https://docs.microsoft.com/dotnet/core/extensions/logging?tabs=command-line#how-filtering-rules-are-applied).
This filtering lets you control the logs that are sent to each registered
provider, including the `OpenTelemetryLoggerProvider`. "OpenTelemetry" is the
[alias](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.provideraliasattribute)
for `OpenTelemetryLoggerProvider`, that may be used in configuring filtering
rules.

The example below defines "Error" as the default `LogLevel`
and also defines "Warning" as the minimum `LogLevel` for a user defined category.

```csharp
ILoggingBuilder.AddFilter<OpenTelemetryLoggerProvider>("*", LogLevel.Error);
ILoggingBuilder.AddFilter<OpenTelemetryLoggerProvider>("category name", LogLevel.Warning);
```

## Learn more

* See also the official guide for
  [Logging in .NET](https://docs.microsoft.com/dotnet/core/extensions/logging).
* [`LogLevel`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel)
