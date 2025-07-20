# Console Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Console.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Console.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)

The console exporter prints data to the Console window.
ConsoleExporter supports exporting logs, metrics and traces.

> [!WARNING]
> This exporter is intended for debugging and learning purposes. It is not
  recommended for production use. The output format is not standardized and can
  change at any time.
  If a standardized format for exporting telemetry to stdout is desired, upvote on
  [this feature request](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5920).

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Console
```

See the individual "getting started" examples depending on the signal being
used:

* Logs: [ASP.NET Core](../../docs/logs/getting-started-aspnetcore/README.md) |
  [Console](../../docs/logs/getting-started-console/README.md)
* Metrics: [ASP.NET
  Core](../../docs/metrics/getting-started-aspnetcore/README.md) |
  [Console](../../docs/metrics/getting-started-console/README.md)
* Traces: [ASP.NET Core](../../docs/trace/getting-started-aspnetcore/README.md)
  | [Console](../../docs/trace/getting-started-console/README.md)

## Configuration

See the
[`TestConsoleExporter.cs`](../../examples/Console/TestConsoleExporter.cs) for
an example of how to use the exporter for exporting traces to a collection.

You can configure the `ConsoleExporter` through `Options` types properties
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
