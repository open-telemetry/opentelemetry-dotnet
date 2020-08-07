# Zipkin Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Zipkin.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Zipkin)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Zipkin.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Zipkin)

## Prerequisite

* [Get Zipkin](https://zipkin.io/pages/quickstart.html)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Zipkin
```

## Configuration

You can configure the `ZipkinExporter` by following the directions below:

* `Endpoint`: Zipkin endpoint address.
* `TimeoutSeconds`: Timeout in seconds.
* `ServiceName`: Name of the service reporting telemetry.
* `UseShortTraceIds`: Value indicating whether short trace id should be used.

See
[`TestZipkinExporter.cs`](../../examples/Console/TestZipkinExporter.cs)
for example use.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Zipkin](https://zipkin.io)
