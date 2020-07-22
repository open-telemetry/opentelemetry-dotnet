# Console Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Console.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Console.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Console)

The console exporter prints data to the Console in a JSON serialized format.

**Note:** this exporter is intended to be used during learning how telemetry
data are created and exported. It is not recommended for any production
environment.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Console
```

## Configuration

You can configure the `ConsoleExporter` by following the directions below:

* `DisplayAsJson`: Boolean to show data as JSON.

See the
[`TestConsoleExporter.cs`](../../samples/Console/TestConsoleExporter.cs)
for an example of how to use the exporter.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
