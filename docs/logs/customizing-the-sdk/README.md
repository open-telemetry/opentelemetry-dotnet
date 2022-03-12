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

`ILogger` implementations have a built-in mechanism to apply log filtering.
This filtering lets you define the minimum
[`LogLevel`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel)
for logs that are sent to each registered provider,
including the `OpenTelemetryLoggerProvider`.
You can use the filtering either in configuration (i.e. appsettings.json) or in code.

### via appsettings.json

The example below uses the `OpenTelemetry`
[alias](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.provideraliasattribute)
for `OpenTelemetryLoggingProvider`.
Here the `OpenTelemetryLoggingProvider` is given a default of "Error" which overrides
the global default "Information". A user defined category has a unique value of "Warning".

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
    },
    "OpenTelemetry": {
      "LogLevel": {
        "Default": "Error",
        "category name": "Warning"
      }
    }
  }
}
```

### via code

The example below defines "Error" as the default `LogLevel`
and also defines "Warning" as the minimum `LogLevel` for a user defined category.

```csharp
ILoggingBuilder.AddFilter<OpenTelemetryLoggerProvider>("*", LogLevel.Error);
ILoggingBuilder.AddFilter<OpenTelemetryLoggerProvider>("category name", LogLevel.Warning);
```

## Learn more

* See also the official guide for
  [Logging in .NET](https://docs.microsoft.com/dotnet/core/extensions/logging).
