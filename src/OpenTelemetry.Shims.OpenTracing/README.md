# OpenTracing Shim for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Shims.OpenTracing.svg)](https://www.nuget.org/packages/OpenTelemetry.Shims.OpenTracing)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Shims.OpenTracing.svg)](https://www.nuget.org/packages/OpenTelemetry.Shims.OpenTracing)

The OpenTelemetry project aims to provide backwards compatibility with the
[OpenTracing](https://opentracing.io) project in order to ease migration of
instrumented codebases.

The OpenTracing Shim for OpenTelemetry .NET is an implementation of an
OpenTracing Tracer providing a compatible shim on top of the OpenTelemetry API.

## Installation

```shell
dotnet add package --prerelease OpenTelemetry.Shims.OpenTracing
```

See
[`TestOpenTracingShim.cs`](../../examples/Console/TestOpenTracingShim.cs)
for an example of how to use the OpenTracing shim.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenTracing](https://opentracing.io)
