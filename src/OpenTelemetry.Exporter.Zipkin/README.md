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

You can enable the the `ZipkinExporter` with the `AddZipkinExporter()`.

You can configure the `ZipkinExporter` through
`ZipkinExporterOptions` properties:

* `ServiceName`: Name of the service reporting telemetry. If the `Resource`
   associated with the telemetry has "service.name" defined, then it'll be
   preferred over this option.
* `Endpoint`: URI address to receive telemetry (default `http://localhost:9411/api/v2/spans`).
* `UseShortTraceIds`: Whether the trace's ID should be shortened before
   sending to Zipkin (default false).
* `MaxPayloadSizeInBytes`: Maximum payload size - for .NET versions
   **other** than 4.5.2 (default 4096).
* `ExportProcessorType`: Whether the exporter should use
  [Batch or Simple exporting processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#built-in-span-processors)
  .
* `BatchExportProcessorOptions`: Configuration options for the batch exporter.
  Only used if ExportProcessorType is set to Batch.

See
[`TestZipkinExporter.cs`](../../examples/Console/TestZipkinExporter.cs)
for example use.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Zipkin](https://zipkin.io)
