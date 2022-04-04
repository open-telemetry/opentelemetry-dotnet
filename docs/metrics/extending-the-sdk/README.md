# Extending the OpenTelemetry .NET SDK

* [Building your own exporter](#exporter)
* [Building your own reader](#reader)
* [Building your own exemplar](#exemplar)
* [References](#references)

## Exporter

OpenTelemetry .NET SDK has provided the following built-in metric exporters:

* [Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
* [InMemory](../../../src/OpenTelemetry.Exporter.InMemory/README.md)
* [OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
* [Prometheus](../../../src/OpenTelemetry.Exporter.Prometheus/README.md)

Custom exporters can be implemented to send telemetry data to places which are
not covered by the built-in exporters:

* Exporters should derive from `OpenTelemetry.BaseExporter<Metric>` (which
  belongs to the [OpenTelemetry](../../../src/OpenTelemetry/README.md) package)
  and implement the `Export` method.
* Exporters can optionally implement the `OnShutdown` method.
* Exporters should not throw exceptions from `Export` and
  `OnShutdown`.
* Exporters are responsible for any retry logic needed by the scenario. The SDK
  does not implement any retry logic.
* Exporters should avoid generating telemetry and causing live-loop, this can be
  done via `OpenTelemetry.SuppressInstrumentationScope`.
* Exporters receives a batch of `Metric`, and each `Metric`
  can contain 1 or more `MetricPoint`s.
  The exporter should perform all actions (e.g. serializing etc.) with
  the `Metric`s and `MetricsPoint`s in the batch before returning control from
  `Export`, once the control is returned, the exporter can no longer make any
  assumptions about the state of the batch or anything inside it.
* Exporters should use `ParentProvider.GetResource()` to get the `Resource`
  associated with the provider.

```csharp
class MyExporter : BaseExporter<Metric>
{
    public override ExportResult Export(in Batch<Metric> batch)
    {
        using var scope = SuppressInstrumentationScope.Begin();

        foreach (var metric in batch)
        {
            Console.WriteLine($"Export: {metric.metric}");

            foreach (ref readonly var metricPoint in metric.GetMetricPoints())
            {
                Console.WriteLine($"Export: {metricPoint.StartTime}");
            }
        }

        return ExportResult.Success;
    }
}
```

A demo exporter which simply writes metric name, metric point start time
and tags to the console is shown [here](./MyExporter.cs).

Apart from the exporter itself, you should also provide extension methods as
shown [here](./MyExporterExtensions.cs). This allows users to add the Exporter
to the `MeterProvider` as shown in the example [here](./Program.cs).

## Reader

Not supported.

## Exemplar

Not supported.

## References
