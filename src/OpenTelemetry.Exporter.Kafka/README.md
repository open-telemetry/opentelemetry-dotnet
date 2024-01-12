# Kafka Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Kafka.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Kafka)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Kafka.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Kafka)

The kafka exporter stores data in a user provided kafka.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Kafka
```

## Configuration

See the
[`TestKafkaExporter.cs`](../../examples/Console/TestKafkaExporter.cs) for
an example of how to use the exporter for exporting traces to a collection.

You can configure the `KafkaExporter` through `Options` types properties
and environment variables.
The `Options` type setters take precedence over the environment variables.

## Environment Variables

The following environment variables can be used to override the default
values of the `PeriodicExportingMetricReaderOptions`
(following the [OpenTelemetry specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.12.0/specification/sdk-environment-variables.md#periodic-exporting-metricreader).

| Environment variable          | `PeriodicExportingMetricReaderOptions` property |
| ------------------------------| ------------------------------------------------|
| `OTEL_METRIC_EXPORT_INTERVAL` | `ExportIntervalMilliseconds`                    |
| `OTEL_METRIC_EXPORT_TIMEOUT`  | `ExportTimeoutMilliseconds`                     |

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
