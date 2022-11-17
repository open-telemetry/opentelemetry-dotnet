# gRPC-based implementation of OTLP Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.OpenTelemetryProtocol.Grpc.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol.Grpc)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.OpenTelemetryProtocol.Grpc.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.OpenTelemetryProtocol.Grpc)

[gRPC-based implementation of OTLP Exporter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md)
implementation.

## Prerequisite

* [Get OpenTelemetry Collector](https://opentelemetry.io/docs/collector/)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol.Grpc
```

## Configuration

You can configure the `OtlpExporter` through `Options` types properties
and environment variables.
The `Options` type setters take precedence over the environment variables.

## Options Properties

* `BatchExportProcessorOptions`: Configuration options for the batch exporter.
  Only used if ExportProcessorType is set to Batch.

* `Endpoint`: Target to which the exporter is going to send traces or metrics.
  The endpoint must be a valid Uri with scheme (http or https) and host, and MAY
  contain a port and path.

* `ExportProcessorType`: Whether the exporter should use [Batch or Simple
  exporting
  processor](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#built-in-span-processors).

* `Headers`: Optional headers for the connection.

* `TimeoutMilliseconds` : Max waiting time for the backend to process a batch.

See the [`TestOtlpExporter.cs`](../../examples/Console/TestOtlpExporter.cs) for
an example of how to use the exporter.

## Environment Variables

The following environment variables can be used to override the default
values of the `OtlpExporterOptions`
(following the [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md)).

| Environment variable          | `OtlpExporterOptions` property        |
| ------------------------------| --------------------------------------|
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `Endpoint`                            |
| `OTEL_EXPORTER_OTLP_HEADERS`  | `Headers`                             |
| `OTEL_EXPORTER_OTLP_TIMEOUT`  | `TimeoutMilliseconds`                 |

The following environment variables can be used to override the default
values of the `PeriodicExportingMetricReaderOptions`
(following the [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.12.0/specification/sdk-environment-variables.md#periodic-exporting-metricreader).

| Environment variable          | `PeriodicExportingMetricReaderOptions` property |
| ------------------------------| ------------------------------------------------|
| `OTEL_METRIC_EXPORT_INTERVAL` | `ExportIntervalMilliseconds`                    |
| `OTEL_METRIC_EXPORT_TIMEOUT`  | `ExportTimeoutMilliseconds`                     |

`FormatException` is thrown in case of an invalid value for any of the
supported environment variables.

## References

* [OpenTelemetry
  Collector](https://github.com/open-telemetry/opentelemetry-collector)
* [OpenTelemetry Project](https://opentelemetry.io/)
* [OTLP proto files](https://github.com/open-telemetry/opentelemetry-proto)
