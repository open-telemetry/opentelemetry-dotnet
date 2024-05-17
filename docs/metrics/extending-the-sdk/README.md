# Extending the OpenTelemetry .NET SDK

* [Building your own exporter](#exporter)
* [Building your own reader](#reader)
* [Building your own exemplar reservoir](#exemplarreservoir)
* [Building your own resource detector](../../resources/README.md#resource-detector)
* [References](#references)

## Exporter

OpenTelemetry .NET SDK has provided the following built-in metric exporters:

* [InMemory](../../../src/OpenTelemetry.Exporter.InMemory/README.md)
* [Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
* [OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
* [Prometheus HttpListener](../../../src/OpenTelemetry.Exporter.Prometheus.HttpListener/README.md)
* [Prometheus AspNetCore](../../../src/OpenTelemetry.Exporter.Prometheus.AspNetCore/README.md)

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

## ExemplarReservoir

> [!NOTE]
> `ExemplarReservoir` is an experimental API only available in pre-release
  builds. For details see:
  [OTEL1004](../../diagnostics/experimental-apis/OTEL1004.md). Please [provide
  feedback](https://github.com/open-telemetry/opentelemetry-dotnet/issues/5629)
  to help inform decisions about what should be exposed stable and when.

Custom [ExemplarReservoir](../customizing-the-sdk/README.md#exemplarreservoir)s
can be implemented to control how `Exemplar`s are recorded for a metric:

* `ExemplarReservoir`s should derive from `FixedSizeExemplarReservoir` (which
  belongs to the [OpenTelemetry](../../../src/OpenTelemetry/README.md) package)
  and implement the `Offer` methods.
* The `FixedSizeExemplarReservoir` constructor accepts a `capacity` parameter to
  control the number of `Exemplar`s which may be recorded by the
  `ExemplarReservoir`.
* The `virtual` `OnCollected` method is called after the `ExemplarReservoir`
  collection operation has completed and may be used to implement cleanup or
  reset logic.
* The `bool` `ResetOnCollect` property on `ExemplarReservoir` is set to `true`
  when delta aggregation temporality is used for the metric using the
  `ExemplarReservoir`.
* The `Offer` and `Collect` `ExemplarReservoir` methods are called concurrently
  by the OpenTelemetry SDK. As such any state required by custom
  `ExemplarReservoir` implementations needs to be managed using appropriate
  thread-safety/concurrency mechanisms (`lock`, `Interlocked`, etc.).
* Custom `ExemplarReservoir` implementations MUST NOT throw exceptions.
  Exceptions thrown in custom implementations MAY lead to unreleased locks and
  undefined behaviors.

The following example demonstrates a custom `ExemplarReservoir` implementation
which records `Exemplar`s for measurements which have the highest value. When
delta aggregation temporality is used the recorded `Exemplar` will be the
highest value for a given collection cycle. When cumulative aggregation
temporality is used the recorded `Exemplar` will be the highest value for the
lifetime of the process.

```csharp
class HighestValueExemplarReservoir : FixedSizeExemplarReservoir
{
    private readonly object lockObject = new();
    private long? previousValueLong;
    private double? previousValueDouble;

    public HighestValueExemplarReservoir()
        : base(capacity: 1)
    {
    }

    public override void Offer(in ExemplarMeasurement<long> measurement)
    {
        if (!this.previousValueLong.HasValue || measurement.Value > this.previousValueLong.Value)
        {
            lock (this.lockObject)
            {
                if (!this.previousValueLong.HasValue || measurement.Value > this.previousValueLong.Value)
                {
                    this.UpdateExemplar(0, in measurement);
                    this.previousValueLong = measurement.Value;
                }
            }
        }
    }

    public override void Offer(in ExemplarMeasurement<double> measurement)
    {
        if (!this.previousValueDouble.HasValue || measurement.Value > this.previousValueDouble.Value)
        {
            lock (this.lockObject)
            {
                if (!this.previousValueDouble.HasValue || measurement.Value > this.previousValueDouble.Value)
                {
                    this.UpdateExemplar(0, in measurement);
                    this.previousValueDouble = measurement.Value;
                }
            }
        }
    }

    protected override void OnCollected()
    {
        if (this.ResetOnCollect)
        {
            lock (this.lockObject)
            {
                this.previousValueLong = null;
                this.previousValueDouble = null;
            }
        }
    }
}
```

Custom [ExemplarReservoir](../customizing-the-sdk/README.md#exemplarreservoir)s
can be configured using the View API. For details see: [Changing the
ExemplarReservoir for a
Metric](../customizing-the-sdk/README.md#changing-the-exemplarreservoir-for-a-metric).

## References
