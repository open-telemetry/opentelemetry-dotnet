# Trace context propagators for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Extensions.Propagators.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Propagators)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Extensions.Propagators.svg)](https://www.nuget.org/packages/OpenTelemetry.Extensions.Propagators)

The package provides context propagators (currently supporting B3 format) for tracing. 

## Installation

```shell
dotnet add package OpenTelemetry.Extensions.Propagators
```

## Configuration

Run `B3 OpenZipkin` context only:

```
Sdk.SetDefaultTextMapPropagator(new B3Propagator())
```

Run `B3 OpenZipkin` and `W3C Baggage` Propagator:

```
Sdk.SetDefaultTextMapPropagator(new CompositeTextMapPropagator(new TextMapPropagator[]
    {
        new B3Propagator(),
        new BaggagePropagator(),
    }));
```


## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [B3 (Zipkin) Context specification](https://github.com/openzipkin/b3-propagation)
