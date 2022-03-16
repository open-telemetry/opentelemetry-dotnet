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
The `SetResourceBuilder` method on `OpenTelemetryLoggerOptions` can be used to
set a single `ResourceBuilder`. If `SetResourceBuilder` is called multiple
times, only the last is kept. It is not possible to change the resource builder
*after* creating the `LoggerFactory`.

The snippet below shows configuring a custom `ResourceBuilder` to the provider.

```csharp
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddOpenTelemetry(options =>
    {
        options.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(
            serviceName: "MyService",
            serviceVersion: "1.0.0"
            ));
    });
});
```

See [Program.cs](Program.cs) for complete example.

It is also possible to configure the `Resource` by using following
environmental variables:

| Environment variable       | Description                                        |
| -------------------------- | -------------------------------------------------- |
| `OTEL_RESOURCE_ATTRIBUTES` | Key-value pairs to be used as resource attributes. See the [Resource SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable) for more details. |
| `OTEL_SERVICE_NAME`        | Sets the value of the `service.name` resource attribute. If `service.name` is also provided in `OTEL_RESOURCE_ATTRIBUTES`, then `OTEL_SERVICE_NAME` takes precedence. |

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
