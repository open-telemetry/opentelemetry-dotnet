# Configuring ILogger Filtering with OpenTelemetry

`ILogger` implementations have a built-in mechanism to apply
[log filtering](https://docs.microsoft.com/dotnet/core/extensions/logging?tabs=command-line#how-filtering-rules-are-applied).
This filtering lets you define the minimum
[`LogLevel`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.loglevel)
for logs that are sent to each registered provider,
including the OpenTelemetry provider.
You can use the filtering either in configuration
(for example, by using an appsettings.json file) or in code.
A full example is shown in [Program.cs](./Program.cs).

## via appsettings.json

This example uses the `OpenTelemetry`
[alias](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.provideraliasattribute)
for `OpenTelemetryLoggingProvider`.
Here the `OpenTelemetryLoggingProvider` is given a default of "Error" which overrides
the global default "Information". A user defined category has a unique value of "Warning".
A full example is shown in [appsettings.json](./appsettings.json).

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

## via code

This example defines "Error" as the default `LogLevel`.
This example defines "Information" as the minimum `LogLevel` for a user defined category.

```csharp
ILoggingBuilder.AddFilter<OpenTelemetryLoggerProvider>("*", LogLevel.Error);
ILoggingBuilder.AddFilter<OpenTelemetryLoggerProvider>("category name", LogLevel.Information);
```

## Learn more

* See also the official guide for [Logging in .NET](https://docs.microsoft.com/dotnet/core/extensions/logging)
