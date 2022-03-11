# Customizing OpenTelemetry .NET SDK for Logs

## OpenTelemetryLoggerProvider

As shown in the [getting-started](../getting-started/README.md) doc, a valid
`ILoggerProvider` must be configured and built to collect logs with OpenTelemetry .NET Sdk.
`OpenTelemetryLoggerProvider` holds all the configuration options.
Naturally, almost all the customizations must be done on the `OpenTelemetryLoggerProvider`.

## Building the OpenTelemetryLoggerProvider

Building the `OpenTelemteryLoggerProvider` is done either by using the static `LoggerFactory.Create()` method or the `IHostBuilder.ConfigureLogging()` method.
In either examples, a user will use the `ILoggerBuilder.AddOpenTelemetry()` extension method.

The snippets below show this concept. This will create the `OpenTelemetryLoggerProvider` with the default configuration, and is not particularly useful.
The subsequent sections show how to build a more useful provider.

```csharp
TODO: VERIFY USINGS
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

LoggerFactory.Create(builder => builder.AddOpenTelemetry());
```

```csharp
TODO: VERIFY USINGS
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

Host.CreateDefaultBuilder().ConfigureLogging(builder => builder.AddOpenTelemetry());
```
## OpenTelemetryLoggerProvider configuration

The following concepts can be viewed in the example app [Program.cs](...)



TODO: ARE THESE LINKS VALID?

 
 Provider holds the logging configuration, which includes the following:

1. The list of
   [Processors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-processor),
   including exporting processors which exports logs to
   [Exporters](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-exporter)
2. The
   [Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
   associated with the logs.
3. Miscellaneous options stored in `OpenTelemetryLoggerOptions`.


### IncludeScopes

A "scope" is an ILogger concept that can group a set of logical operations and attach
data to each log created as part of a set.

`IncludeScope` will include all scopes with the exported `LogRecord`.

The following example demonstates `ConsoleExporter` output with `IncludeScope = true`:

```
using(logger.BeginScope("My Scope 1"))
using(logger.BeginScope("My Scope 2"))
{
    logger.LogInformation("Hello Information within scope");
}
```

```
LogRecord.TraceId:            00000000000000000000000000000000
LogRecord.SpanId:             0000000000000000
LogRecord.Timestamp:          0001-01-01T00:00:00.0000000Z
LogRecord.EventId:            0
LogRecord.EventName:
LogRecord.CategoryName:       ConsoleApp
LogRecord.LogLevel:           Information
LogRecord.TraceFlags:         None
LogRecord.State:              Hello Information within scope
LogRecord.ScopeValues (Key:Value):
[Scope.0]:                              My Scope 1
[Scope.1]:                              My Scope 2
```

### IncludeFormattedMessage

ILogger supports message templates which can contain placeholders for arguments provided as parameters.

`IncludeFormattedMessage` determines if the fully formatted message is included with
the exported `LogRecord.FormattedMessage`.

Consider the following example:
```
logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
// If options.IncludeFormattedMessage == true
// The output LogRecord.FormattedMessage = "Hello from tomato 2.99".
// If options.IncludeFormattedMessage == false
// The output LogRecord.FormattedMessage = null.
```


- `options.IncludeFormattedMessage = true`
    ```
    LogRecord.TraceId:            00000000000000000000000000000000
    LogRecord.SpanId:             0000000000000000
    LogRecord.Timestamp:          0001-01-01T00:00:00.0000000Z
    LogRecord.EventId:            0
    LogRecord.EventName:
    LogRecord.CategoryName:       LogDemo_Program
    LogRecord.LogLevel:           Information
    LogRecord.TraceFlags:         None
    LogRecord.FormattedMessage:   Hello from tomato 2.99.
    LogRecord.State:              Hello from tomato 2.99.
    ```
- `options.IncludeFormattedMessage = false`
    ```
    LogRecord.TraceId:            00000000000000000000000000000000
    LogRecord.SpanId:             0000000000000000
    LogRecord.Timestamp:          0001-01-01T00:00:00.0000000Z
    LogRecord.EventId:            0
    LogRecord.EventName:
    LogRecord.CategoryName:       LogDemo_Program
    LogRecord.LogLevel:           Information
    LogRecord.TraceFlags:         None
    LogRecord.State:              Hello from tomato 2.99.
    ```

### ParseStateValues

TODO

ILogger supports message templates which can contain placeholders for arguments provided as parameters.

`ParseStateValues` determines if the arguments will be individually enumeratod in the exported `LogRecord.StateValues`.


Consider the following example:
```
logger.LogInformation("Hello from {name} {price}.", "tomato", 2.99);
```


- `options.ParseStateValues = true`
    ```
    LogRecord.TraceId:            00000000000000000000000000000000
    LogRecord.SpanId:             0000000000000000
    LogRecord.Timestamp:          0001-01-01T00:00:00.0000000Z
    LogRecord.EventId:            0
    LogRecord.EventName:
    LogRecord.CategoryName:       LogDemo_Program
    LogRecord.LogLevel:           Information
    LogRecord.TraceFlags:         None
    LogRecord.StateValues (Key:Value):
    name                          tomato
    price                         2.99
    {OriginalFormat}              Hello from {name} {price}.
    ```

- `options.ParseStateValues = false`
    ```
    LogRecord.TraceId:            00000000000000000000000000000000
    LogRecord.SpanId:             0000000000000000
    LogRecord.Timestamp:          0001-01-01T00:00:00.0000000Z
    LogRecord.EventId:            0
    LogRecord.EventName:
    LogRecord.CategoryName:       LogDemo_Program
    LogRecord.LogLevel:           Information
    LogRecord.TraceFlags:         None
    LogRecord.State:              Hello from tomato 2.99.
    ```


### AddProcessor

TODO

A [Processor](...) is run on data between being received and exported.
Generally, a processor pre-processes data before it is exported (e.g. modify attributes or sample) or helps ensure that data makes it through a pipeline successfully (e.g. batch/retry).

For more information on Processors, please see [Extending the SDK](../extending-the-sdk/README.md#processor)

https://opentelemetry.io/docs/collector/configuration/#processors

https://github.com/open-telemetry/opentelemetry-collector/tree/main/processor#recommended-processors

### SetResourceBuilder

TODO

A [Resource](...) is an immutable representation of the entity producing
telemetry as [Attributes](../common/common.md#attributes).

https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md

https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/overview.md#resources


Resources are part of the output.
```
options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
    serviceName: "MyService",
    serviceVersion: "1.0.0"
    ));
```

Consider the following example:
```
LogRecord.TraceId:            00000000000000000000000000000000
LogRecord.SpanId:             0000000000000000
LogRecord.Timestamp:          0001-01-01T00:00:00.0000000Z
LogRecord.EventId:            0
LogRecord.EventName:
LogRecord.CategoryName:       LogDemo_Program
LogRecord.LogLevel:           Information
LogRecord.TraceFlags:         None
LogRecord.State:              Hello Information within scope
LogRecord.ScopeValues (Key:Value):
[Scope.0]:                              My Scope 1
[Scope.1]:                              My Scope 2
Resource associated with LogRecord:
    service.name: MyService
    service.version: 1.0.0
    service.instance.id: 00000000-0000-0000-0000-000000000000
```

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

* See also the official guide for [Logging in .NET](https://docs.microsoft.com/dotnet/core/extensions/logging)
