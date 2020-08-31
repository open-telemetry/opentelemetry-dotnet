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
not covered by the built-in exporters.

Here is the guidance for writing a custom exporter:

* Exporters should derive from `ActivityExporter` (which belongs to the
  [OpenTelemetry](https://www.nuget.org/packages/opentelemetry) package) and
  implement `Export` method.
* Exporters can optionally implement `OnShutdown`.
* Depending on user's choice and load on the application, `Export` may get
  called with one or more activities.
* Exporters will only receive sampled-in and ended activities.
* Exporters should not throw exceptions.
* Exporters should not modify activities they receive (the same activity may be
  exported again by different exporter).
* Exporters are responsible for any retry logic needed by the scenario. The SDK
  does not implement any retry logic.

A demo exporter which simply writes activity name to the console is shown
[here](./MyExporter.cs).

Apart from the exporter itself, you should also provide extension methods as
shown [here](./MyExporterHelperExtensions.cs). This allows users to add the
Exporter to the `TracerProvider` as shown in the sample code
[here](./Program.cs).

To run the full example code demonstrating the exporter, run the following
command from this folder.

```sh
dotnet run
```

## Instrumentation Library

TBD

## Processor

OpenTelemetry .NET SDK has provided the following built-in processors:

* [BatchExportActivityProcessor](../../../src/OpenTelemetry/Trace/BatchExportActivityProcessor.cs)
* [CompositeActivityProcessor](../../../src/OpenTelemetry/Trace/CompositeActivityProcessor.cs)
* [ReentrantExportActivityProcessor](../../../src/OpenTelemetry/Trace/ReentrantExportActivityProcessor.cs)
* [SimpleExportActivityProcessor](../../../src/OpenTelemetry/Trace/SimpleExportActivityProcessor.cs)

## Sampler

OpenTelemetry .NET SDK has provided the following built-in samplers:

* [AlwaysOffSampler](../../../src/OpenTelemetry/Trace/AlwaysOffSampler.cs)
* [AlwaysOnSampler](../../../src/OpenTelemetry/Trace/AlwaysOnSampler.cs)
* [ParentBasedSampler](../../../src/OpenTelemetry/Trace/ParentBasedSampler.cs)
* [TraceIdRatioBasedSampler](../../../src/OpenTelemetry/Trace/TraceIdRatioBasedSampler.cs)

Custom samplers can be implemented to cover more scenarios:

* Samplers should inherit from `Sampler`, and implement `ShouldSample` method.
* `ShouldSample` should not block or take long time, since it will be called on
  critical code path.

```csharp
internal class MySampler : Sampler
{
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        return new SamplingResult(SamplingDecision.RecordAndSampled);
    }
}
```

## References

* [Exporter
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#span-exporter)
