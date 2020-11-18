# In-memory Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.InMemory.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.InMemory)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.InMemory.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.InMemory)

The in-memory exporter stores data in a user provided memory buffer.

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.InMemory
```

## Configuration

```csharp
var activityList = new List<Activity>();
var activityExporter = new InMemoryExporter<Activity>(activityList);

var logRecordList = new List<LogRecord>();
var logExporter = new InMemoryExporter<LogRecord>(logRecordList);
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
