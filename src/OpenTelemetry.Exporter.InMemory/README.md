# In-memory Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.InMemory.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.InMemory)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.InMemory.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.InMemory)

The in-memory exporter stores data in a memory buffer.

**Note:** this exporter is intended to be used for testing purpose. It is not
recommended for any production environment.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.InMemory
```

## Configuration

```csharp
var list = new List<object>();
var activityExporter = new InMemoryExporter<Activity>(new InMemoryExporterOptions { Collection = list });
var logExporter = new InMemoryExporter<LogRecord>(new InMemoryExporterOptions { Collection = list });
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
