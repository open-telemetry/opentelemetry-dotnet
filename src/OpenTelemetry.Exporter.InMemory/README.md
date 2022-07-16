# In-memory Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.InMemory.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.InMemory)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.InMemory.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.InMemory)

The in-memory exporter stores data in a user provided memory buffer.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.InMemory
```

## Configuration

See the
[`TestInMemoryExporter.cs`](../../examples/Console/TestInMemoryExporter.cs) for
an example of how to use the exporter for exporting traces to a collection.

You can configure the `InMemoryExporter` through `Options` types properties
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

`FormatException` is thrown in case of an invalid value for any of the
supported environment variables.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
