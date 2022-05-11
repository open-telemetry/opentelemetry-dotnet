# Trace context propagators for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Propagators.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Propagators)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Propagators.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Propagators)

The package provides context propagators (currently supporting B3 format) for tracing.

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
using OpenTelemetry.Extensions.Propagators;

Sdk.SetDefaultTextMapPropagator(new B3Propagator())
```

Use `B3 OpenZipkin` and `W3C Baggage` propagators at the same time:

```csharp
using OpenTelemetry.Extensions.Propagators;

Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
    {
        new B3Propagator(),
        new BaggagePropagator(),
    }));
```

## References

* [B3 (Zipkin) Context specification](https://github.com/openzipkin/b3-propagation)

* [OpenTelemetry Project](https://opentelemetry.io/)
