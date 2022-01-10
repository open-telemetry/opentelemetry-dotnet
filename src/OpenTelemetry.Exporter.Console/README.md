# Console Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Console.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Console.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)

The console exporter prints data to the Console window.
ConsoleExporter supports exporting logs, metrics and traces.

**Note:** this exporter is intended to be used during learning how telemetry
data are created and exported. It is not recommended for any production
environment.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Console
```

See the individual "getting started" examples depending on the signal being
used:

* [Logs](../../docs/logs/getting-started/Program.cs)
* [Metrics](../../docs/metrics/getting-started/Program.cs)
* [Traces](../../docs/trace/getting-started/Program.cs)

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
