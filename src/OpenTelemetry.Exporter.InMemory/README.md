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

## Remarks

When working with `Metric` it's important to note that by design the
MetricApi reuses Metrics (see also: `MetricReader.metricsCurrentBatch`).
This means that after multiple exports, the `InMemoryExporter` will
repeatedly export the same instance(s) of `Metric`.

It is recommended to clear the exported collection before repeated flushes to
avoid working with duplicates.

```csharp
var exportedItems = new List<Metric>();
using var meter = new Meter(Utils.GetCurrentMethodName());
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(meter.Name)
    .AddInMemoryExporter(exportedItems)
    .Build();

// ...

meterProvider.ForceFlush(); // first flush, exportedItems is empty.

// ...

exportedItems.Clear();
meterProvider.ForceFlush();
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
