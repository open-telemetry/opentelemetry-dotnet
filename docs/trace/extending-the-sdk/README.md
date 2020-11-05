# Extending the OpenTelemetry .NET SDK

- [Extending the OpenTelemetry .NET SDK](#extending-the-opentelemetry-net-sdk)
  - [Exporter](#exporter)
  - [Instrumentation Library](#instrumentation-library)
    - [Writing instrumentation library](#writing-instrumentation-library)
  - [Processor](#processor)
  - [Sampler](#sampler)
  - [References](#references)

## Exporter

OpenTelemetry .NET SDK has provided the following built-in trace exporters:

* [Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
* [InMemory](../../../src/OpenTelemetry.Exporter.InMemory/README.md)
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

[Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library)
denotes the library that provides the instrumentation for a given [Instrumented
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumented-library).

The [OpenTelemetry .NET Github
repo](https://github.com/open-telemetry/opentelemetry-dotnet) ships the following instrumentation libraries.

* [ASP.NET](./src/OpenTelemetry.Instrumentation.AspNet/README.md)
* [ASP.NET Core](./src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
* [gRPC client](./src/OpenTelemetry.Instrumentation.GrpcNetClient/README.md)
* [HTTP clients](./src/OpenTelemetry.Instrumentation.Http/README.md)
* [Redis
  client](./src/OpenTelemetry.Instrumentation.StackExchangeRedis/README.md)
* [SQL client](./src/OpenTelemetry.Instrumentation.SqlClient/README.md)

### Writing instrumentation library

This section describes the steps required to write your own instrumentation library.

If you are writing a new library or modifying an existing library, the recommendation is to use ActivitySource API/OpenTelemetry API to instrument it and emit activity/span.
If the instrumented library is instrumented using ActivitySource API, then there is no need of
writing a separate instrumentation library, as instrumented and instrumentation library become
same in this case. For applications to collect traces from this library, all that is needed is to enable the ActivitySource for the library using `AddSource` method of the `TracerProviderBuilder`.

It is not always possible to modify an existing library. For those libraries, it is required to write an
Instrumentation Library. The instrumentation library in this case must use ActivitySource API and
emit activity/span. The mechanics of how the instrumentation library works depends on each library. For example, StackExchaneRedis library allows hooks into the library, and the instrumentation library in this case, leverages them, and emits Span/Activity, on behalf of the instrumented library.
The instrumentation library must provide extension methods on `TracerProviderBuilder`, to enable the instrumentation. Inside the extension method, the ActivitySource from the instrumentation library must be enabled using `AddSource`. Additionally, if the instrumentation library expects its lifetime to be managed along with the `TracerProvider`, then register itself with the provider using the `AddInstrumentation` method. If the instrumentation is `IDisposable`, it'll be disposed automatically when the TracerProvider itself is disposed.
(Link to Redis example.)

There is a special case for libraries which are already instrumented with [Activity](https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md), but using the [DiagnosticSource](https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md) method. These libraries already emit activities, but it may not conform to the OpenTelemetry semantic conventions. Also, as these libraries do not use ActivitySource to create Activity, they cannot be simply enabled.
For this case, the recommended approach is to write instrumentation library which subscribe to the DiagnosticSource events from the instrumented library, and in turn produce *new* activity using ActivitySource. This new activity must be created as a sibling of the activity already produced by the library. i.e the new activity must have the same parent as the original activity.
Some common examples of such libraries include Asp.Net, Asp.Net Core, HttpClient (.NET Core). Instrumentation libraries for these are already provided in this repo.

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
