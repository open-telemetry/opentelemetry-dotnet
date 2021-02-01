# OTLP Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.OpenTelemetryProtocol.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.OpenTelemetryProtocol.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol)

The OTLP (OpenTelemetry Protocol) exporter communicates to an OpenTelemetry
Collector through a gRPC protocol.

## Prerequisite

* [Get OpenTelemetry Collector](https://opentelemetry.io/docs/collector/)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

## Configuration

You can configure the `OtlpExporter` through `OtlpExporterOptions` properties:

* `Endpoint`: Target to which the exporter is going to send traces or metrics.
* `Credentials`: Client-side channel credentials.
* `Headers`: Optional headers for the connection.
* `ChannelOptions`: gRPC channel options.
* `ExportProcessorType`: Whether the exporter should use
  [Batch or Simple exporting processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#built-in-span-processors)
  .
* `BatchExportProcessorOptions`: Configuration options for the batch exporter.
  Only used if ExportProcessorType is set to Batch.

See the
[`TestOtlpExporter.cs`](../../examples/Console/TestOtlpExporter.cs)
for an example of how to use the exporter.

## References

* [OpenTelemetry
  Collector](https://github.com/open-telemetry/opentelemetry-collector)
* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenTelemetry
  Protocol](https://github.com/open-telemetry/opentelemetry-proto)
