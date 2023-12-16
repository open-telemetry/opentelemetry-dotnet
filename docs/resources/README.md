# Resources

Quick links:

* [Building your own resource detector](#resource-detector)

[Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
is the immutable representation of the entity producing the telemetry. If no
`Resource` is explicitly configured, the
[default](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#semantic-attributes-with-sdk-provided-default-value)
is to use a resource indicating this
[Service](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#service)
and [Telemetry
SDK](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#telemetry-sdk).
The `ConfigureResource` method on `TracerProviderBuilder` can be used to
configure the resource on the provider. `ConfigureResource` accepts an `Action`
to configure the `ResourceBuilder`. Multiple calls to `ConfigureResource` can be
made. When the provider is built, it builds the final `Resource` combining all
the `ConfigureResource` calls. There can only be a single `Resource` associated
with a provider. It is not possible to change the resource builder *after* the
provider is built, by calling the `Build()` method on the
`TracerProviderBuilder`.

`ResourceBuilder` offers various methods to construct resource comprising of
multiple attributes from various sources. Examples include `AddTelemetrySdk()`
which adds [Telemetry
Sdk](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#telemetry-sdk)
resource, and `AddService()` which adds
[Service](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#service)
resource. It also allows adding `ResourceDetector`s.

Follow [this](#resource-detector) document
to learn about writing custom resource detectors.

The snippet below shows configuring the `Resource` associated with the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resourceBuilder => resourceBuilder.AddTelemetrySdk())
    .ConfigureResource(resourceBuilder => resourceBuilder.AddService("service-name"))
    .Build();
```

It is also possible to configure the `Resource` by using following
environmental variables:

| Environment variable       | Description                                        |
| -------------------------- | -------------------------------------------------- |
| `OTEL_RESOURCE_ATTRIBUTES` | Key-value pairs to be used as resource attributes. See the [Resource SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable) for more details. |
| `OTEL_SERVICE_NAME`        | Sets the value of the `service.name` resource attribute. If `service.name` is also provided in `OTEL_RESOURCE_ATTRIBUTES`, then `OTEL_SERVICE_NAME` takes precedence. |

## Resource Detector

OpenTelemetry .NET SDK provides a resource detector for detecting resource
information from the `OTEL_RESOURCE_ATTRIBUTES` and `OTEL_SERVICE_NAME`
environment variables.

Custom resource detectors can be implemented:

* ResourceDetectors should inherit from
  `OpenTelemetry.Resources.IResourceDetector`, (which belongs to the
  [OpenTelemetry](../../src/OpenTelemetry/README.md) package), and implement
  the `Detect` method.

A demo ResourceDetector is shown [here](../trace/extending-the-sdk/MyResourceDetector.cs).
