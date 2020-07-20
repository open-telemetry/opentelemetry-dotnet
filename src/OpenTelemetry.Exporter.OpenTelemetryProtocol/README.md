# OTLP Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.OpenTelemetryProtocol.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.OpenTelemetryProtocol.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)

The OTLP (OpenTelemetry Protocol) exporter communicates to an OpenTelemetry
Collector through a gRPC protocol.

## Prerequisite

* [Get OpenTelemetry Collector](https://opentelemetry.io/docs/collector/about/)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

## Configuration

You can configure the `OtlpExporter` by following the directions below:

* `Endpoint`: Target to which the exporter is going to send traces or metrics.
* `Credentials`: Client-side channel credentials.
* `Headers`: Optional headers for the connection.

See the
[`TestOtlpExporter.cs`](../../samples/Exporters/Console/TestOtlpExporter.cs)
for an example of how to use the exporter.

## References

* [OpenTelemetry
  Collector](https://github.com/open-telemetry/opentelemetry-collector)
* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenTelemetry
  Protocol](https://github.com/open-telemetry/opentelemetry-proto)
  