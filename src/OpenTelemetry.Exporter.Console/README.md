# Console Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Console.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Console.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)

The console exporter prints data to the Console window.
ConsoleExporter supports exporting both traces and logs.

**Note:** this exporter is intended to be used during learning how telemetry
data are created and exported. It is not recommended for any production
environment.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Console
```

See the
[`TestConsoleExporter.cs`](../../examples/Console/TestConsoleExporter.cs) for an
example of how to use the exporter for exporting traces.

See the [Program](../../docs/logs/getting-started/Program.cs) for
an example of how to use the exporter for exporting logs.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
