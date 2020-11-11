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

You can configure the `ZipkinExporter` with the following options:

* `ServiceName`: Name of the service reporting telemetry.
* `Endpoint`: URI address to receive telemetry.
* `UseShortTraceIds`: Whether the trace's ID should be shortened before
   sending to Zipkin (default false).
* `MaxPayloadSizeInBytes`: Maximum payload size - for .NET versions
   **other** than 4.5.2 (default 4096).

See
[`TestZipkinExporter.cs`](../../examples/Console/TestZipkinExporter.cs)
for example use.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Zipkin](https://zipkin.io)
