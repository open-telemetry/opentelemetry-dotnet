# OpenTracing Shim for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Shims.OpenTracing.svg)](https://www.nuget.org/packages/OpenTelemetry.Shims.OpenTracing)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Shims.OpenTracing.svg)](https://www.nuget.org/packages/OpenTelemetry.Shims.OpenTracing)

> [!IMPORTANT]
> This package is [Deprecated](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/versioning-and-stability.md)
> and it will stop receiving any updates in March 2027.
> Use the OpenTelemetry API and SDK directly instead of the OpenTracing shims.

The OpenTelemetry project aims to provide backwards compatibility with the
[OpenTracing](https://opentracing.io) project in order to ease migration of
instrumented codebases.

The OpenTracing Shim for OpenTelemetry .NET is an implementation of an
OpenTracing Tracer providing a compatible shim on top of the OpenTelemetry API.

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.Shims.OpenTracing
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenTracing](https://opentracing.io)
