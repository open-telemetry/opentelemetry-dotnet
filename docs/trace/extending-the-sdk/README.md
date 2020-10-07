# Extending the OpenTelemetry .NET SDK

* [Building your own exporter](#exporter)
* [Building your own instrumentation library](#instrumentation-library)
* [Building your own processor](#processor)
* [Building your own sampler](#sampler)
* [References](#references)

## Exporter

OpenTelemetry .NET SDK has provided the following built-in exporters:

* [Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
* [Jaeger](../../../src/OpenTelemetry.Exporter.Jaeger/README.md)
* [OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
* [Zipkin](../../../src/OpenTelemetry.Exporter.Zipkin/README.md)

Custom exporters can be implemented to send telemetry data to places which are
not covered by the built-in exporters:

* Exporters should derive from `OpenTelemetry.BaseExporter<Activity>` (which
  belongs to the [OpenTelemetry](../../../src/OpenTelemetry/README.md) package)
  and implement the `Export` method.
* Exporters can optionally implement the `OnShutdown` method.
* Depending on user's choice and load on the application, `Export` may get
  called with one or more activities.
* Exporters will only receive sampled-in and ended activities.
* Exporters should not throw exceptions from `Export` and `OnShutdown`.
* Exporters should not modify activities they receive (the same activity may be
  exported again by different exporter).
* Exporters are responsible for any retry logic needed by the scenario. The SDK
  does not implement any retry logic.
* Exporters should avoid generating telemetry and causing live-loop, this can be
  done via `OpenTelemetry.SuppressInstrumentationScope`.

```csharp
class MyExporter : BaseExporter<Activity>
{
    public override ExportResult Export(in Batch<Activity> batch)
    {
        using var scope = SuppressInstrumentationScope.Begin();

        foreach (var activity in batch)
        {
            Console.WriteLine($"Export: {activity.DisplayName}");
        }

        return ExportResult.Success;
    }
}
```

A demo exporter which simply writes activity name to the console is shown
[here](./MyExporter.cs).

Apart from the exporter itself, you should also provide extension methods as
shown [here](./MyExporterHelperExtensions.cs). This allows users to add the
Exporter to the `TracerProvider` as shown in the example [here](./Program.cs).

## Instrumentation Library

TBD

## Processor

OpenTelemetry .NET SDK has provided the following built-in processors:

* [BatchExportProcessor&lt;T&gt;](../../../src/OpenTelemetry/BatchExportProcessor.cs)
* [CompositeProcessor&lt;T&gt;](../../../src/OpenTelemetry/CompositeProcessor.cs)
* [ReentrantExportProcessor&lt;T&gt;](../../../src/OpenTelemetry/ReentrantExportProcessor.cs)
* [SimpleExportProcessor&lt;T&gt;](../../../src/OpenTelemetry/SimpleExportProcessor.cs)

Custom processors can be implemented to cover more scenarios:

* Processors should inherit from `OpenTelemetry.BaseProcessor<Activity>` (which
  belongs to the [OpenTelemetry](../../../src/OpenTelemetry/README.md) package),
  and implement the `OnStart` and `OnEnd` methods.
* Processors can optionally implement the `OnForceFlush` and `OnShutdown`
  methods. `OnForceFlush` should be thread safe.
* Processors should not throw exceptions from `OnStart`, `OnEnd`, `OnForceFlush`
  and `OnShutdown`.
* `OnStart` and `OnEnd` should be thread safe, and should not block or take long
  time, since they will be called on critical code path.

```csharp
class MyProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
        Console.WriteLine($"OnStart: {activity.DisplayName}");
    }

    public override void OnEnd(Activity activity)
    {
        Console.WriteLine($"OnEnd: {activity.DisplayName}");
    }
}
```

A demo processor is shown [here](./MyProcessor.cs).

## Sampler

OpenTelemetry .NET SDK has provided the following built-in samplers:

* [AlwaysOffSampler](../../../src/OpenTelemetry/Trace/AlwaysOffSampler.cs)
* [AlwaysOnSampler](../../../src/OpenTelemetry/Trace/AlwaysOnSampler.cs)
* [ParentBasedSampler](../../../src/OpenTelemetry/Trace/ParentBasedSampler.cs)
* [TraceIdRatioBasedSampler](../../../src/OpenTelemetry/Trace/TraceIdRatioBasedSampler.cs)

Custom samplers can be implemented to cover more scenarios:

* Samplers should inherit from `OpenTelemetry.Trace.Sampler` (which belongs to
  the [OpenTelemetry](../../../src/OpenTelemetry/README.md) package), and
  implement the `ShouldSample` method.
* `ShouldSample` should be thread safe, and should not block or take long time,
  since it will be called on critical code path.

```csharp
class MySampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        return new SamplingResult(SamplingDecision.RecordAndSampled);
    }
}
```

A demo sampler is shown [here](./MySampler.cs).

## References

* [Exporter
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#span-exporter)
* [Processor
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#span-processor)
* [Sampler
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#sampler)
