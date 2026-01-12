# Propagator formats for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Propagators.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Propagators)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Propagators.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Propagators)

The package provides context
propagators by following the [OpenTelemetry specification](https://opentelemetry.io/docs/reference/specification/context/api-propagators/)
(currently supporting [B3](https://github.com/openzipkin/b3-propagation) format)
for tracing.

> **Note:** `JaegerPropagator` is deprecated and will be removed in a future version.
> The Jaeger propagation format has been deprecated in favor of W3C Trace Context.
> Use `TraceContextPropagator` from the `OpenTelemetry` package instead.
> See [Jaeger's migration guide](https://www.jaegertracing.io/sdk-migration/#propagation-format)
> and [OpenTelemetry specification issue #4827](https://github.com/open-telemetry/opentelemetry-specification/issues/4827)
> for more information.

## Installation

Add a reference to the
[`OpenTelemetry.Extensions.Propagators`](https://www.nuget.org/packages/OpenTelemetry.Extensions.Propagators)
package in your project.

```shell
dotnet add package OpenTelemetry.Extensions.Propagators
```

## Configuration

Use `B3 OpenZipkin` context only:

```csharp
using OpenTelemetry;
using OpenTelemetry.Extensions.Propagators;

Sdk.SetDefaultTextMapPropagator(new B3Propagator());
```

Use `B3 OpenZipkin` and `W3C Baggage` propagators at the same time:

```csharp
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Extensions.Propagators;

Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
    {
        new OpenTelemetry.Extensions.Propagators.B3Propagator(),
        new BaggagePropagator(),
    }));
```

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry.Extensions.Propagators" for its internal logging.
Please refer to [SDK
troubleshooting](../OpenTelemetry/README.md#troubleshooting) for instructions on
seeing these internal logs.

## References

* [B3 (Zipkin) Context specification](https://github.com/openzipkin/b3-propagation)
* [OpenTelemetry Project](https://opentelemetry.io/)
