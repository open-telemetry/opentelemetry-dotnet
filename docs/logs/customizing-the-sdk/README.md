# Customizing OpenTelemetry .NET SDK for Logs

## OpenTelemetryLoggerProvider

TODO

## Building the OpenTelemetryLoggerProvider

TODO

## OpenTelemetryLoggerProvider configuration

TODO

### IncludeScopes

Log
[scope](https://docs.microsoft.com/dotnet/core/extensions/logging#log-scopes) is
an `ILogger` concept that can group a set of logical operations and attach data
to each log created as part of a set.

`IncludeScopes` is off by default. Setting this to `true` will include all
scopes with the exported `LogRecord`. Consult the individual `Exporter`
docs to learn more about how scopes will be processed.

See [Program.cs](Program.cs) for an example.

> [!NOTE]
> When using [`ILogger.BeginScope<TState>(TState
state)`](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger.beginscope),
it is highly recommended to use `IReadOnlyList<KeyValue<string, object?>>` or
`List<KeyValuePair<string, object?>>` as the `TState` for the best performance.
When performance is not a critical requirement,
`IEnumerable<KeyValuePair<string, object?>>` can be used.

### IncludeFormattedMessage

`IncludeFormattedMessage` indicates if the `LogRecord.FormattedMessage` will be
set by invoking the formatter from [ILogger.Log](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger.log).
`IncludeFormattedMessage` is `false` by default.

### ParseStateValues

TODO

### AddProcessor

[Processors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/sdk.md#logrecordprocessor)
must be added using `OpenTelemetryLoggerOptions.AddProcessor()`.
It is not supported to add Processors after building the `LoggerFactory`.

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.AddProcessor(...);
    });
});
```

For more information on Processors, please review [Extending the SDK](../extending-the-sdk/README.md#processor)

### SetResourceBuilder

[Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
is the immutable representation of the entity producing the telemetry.
If no `Resource` is explicitly configured, the
[default](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#semantic-attributes-with-sdk-provided-default-value)
is to use a resource indicating this
[Service](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#service)
and [Telemetry
SDK](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#telemetry-sdk).
The `SetResourceBuilder` method on `OpenTelemetryLoggerOptions` can be used to
set a single `ResourceBuilder`. If `SetResourceBuilder` is called multiple
times, only the last is kept. It is not possible to change the resource builder
*after* creating the `LoggerFactory`.

The snippet below shows configuring a custom `ResourceBuilder` to the provider.

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(logging =>
    {
        logging.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
            serviceName: "MyService",
            serviceVersion: "1.0.0"));
    });
});
```

See [Program.cs](Program.cs) for complete example.

It is also possible to configure the `Resource` by using following
environmental variables:

| Environment variable | Description |
| -------------------------- |-------------------------------------------------- |
| `OTEL_RESOURCE_ATTRIBUTES` | Key-value pairs to be used as resource attributes. See the [Resource SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable) for more details. |
| `OTEL_SERVICE_NAME` | Sets the value of the `service.name` resource attribute. If `service.name` is also provided in `OTEL_RESOURCE_ATTRIBUTES`, then `OTEL_SERVICE_NAME` takes precedence. |

## Log Filtering

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
These rules as defined only apply to the `OpenTelemetryLoggerProvider`.

```csharp
builder.AddFilter<OpenTelemetryLoggerProvider>("*", LogLevel.Error);
builder.AddFilter<OpenTelemetryLoggerProvider>("MyProduct.MyLibrary.MyClass", LogLevel.Warning);
```

## Learn more

* See also the official guide for
  [Logging in .NET](https://docs.microsoft.com/dotnet/core/extensions/logging).
