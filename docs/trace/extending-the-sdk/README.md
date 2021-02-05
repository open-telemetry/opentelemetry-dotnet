# Extending the OpenTelemetry .NET SDK

* [Building your own exporter](#exporter)
* [Building your own instrumentation library](#instrumentation-library)
* [Building your own processor](#processor)
* [Building your own sampler](#sampler)
* [References](#references)

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

The [inspiration of the OpenTelemetry
project](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/overview.md#instrumentation-libraries)
is to make every library observable out of the box by having
them call OpenTelemetry API directly. However, many libraries will not have such
integration, and as such there is a need for a separate library which would
inject such calls, using mechanisms such as wrapping interfaces, subscribing to
library-specific callbacks, or translating existing telemetry into OpenTelemetry
model.

A library which enables instrumentation for another library is called
[Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library)
and the library it instruments is called the [Instrumented
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumented-library).
If a given library has built-in instrumentation with OpenTelemetry, then
instrumented library and instrumentation library will be the same.

The [OpenTelemetry .NET Github repo](../../../README.md#getting-started) ships
the following instrumentation libraries. The individual docs for them describes
the library they instrument, and steps for enabling them.

* [ASP.NET](../../../src/OpenTelemetry.Instrumentation.AspNet/README.md)
* [ASP.NET Core](../../../src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
* [gRPC client](../../../src/OpenTelemetry.Instrumentation.GrpcNetClient/README.md)
* [HTTP clients](../../../src/OpenTelemetry.Instrumentation.Http/README.md)
* [Redis
  client](../../../src/OpenTelemetry.Instrumentation.StackExchangeRedis/README.md)
* [SQL client](../../../src/OpenTelemetry.Instrumentation.SqlClient/README.md)

### Writing own instrumentation library

This section describes the steps required to write your own instrumentation
library.

*If you are writing a new library or modifying an existing library, the
recommendation is to use [ActivitySource API/OpenTelemetry
API](../../../src/OpenTelemetry.Api/README.md#introduction-to-opentelemetry-net-tracing-api)
to instrument it and emit activity/span. If a library is instrumented using
ActivitySource API, then there is no need of writing a separate instrumentation
library, as instrumented and instrumentation library become same in this case.
For applications to collect traces from this library, all that is needed is to
enable the ActivitySource for the library using `AddSource` method of the
`TracerProviderBuilder`. The following section is applicable only if you are
writing an instrumentation library for an instrumented library which you cannot
modify to emit activities directly.*

Writing an instrumentation library typically involves 3 steps.

1. First step involves "hijacking" into the target library. The exact mechanism
   of this depends on the target library itself. For example, StackExchangeRedis
   library allows hooks into the library, and the [StackExchangeRedis
   instrumentation
   library](../../../src/OpenTelemetry.Instrumentation.StackExchangeRedis/README.md)
   in this case, leverages them. Another example is System.Data.SqlClient for
   .NET Framework, which publishes events using `EventSource`. The [SqlClient
   instrumentation
   library](../../../src/OpenTelemetry.Instrumentation.SqlClient/Implementation/SqlEventSourceListener.netfx.cs),
   in this case subscribes to the `EventSource` callbacks

2. Second step is to emit activities using the [ActivitySource
   API](../../../src/OpenTelemetry.Api/README.md#introduction-to-opentelemetry-net-tracing-api).
   In this step, the instrumentation library emits activities *on behalf of* the
   target instrumented library. Irrespective of the actual mechanism used in
   first step, this should be uniform across all instrumentation libraries. The
   `ActivitySource` must be created using the name and version of the
   instrumentation library (eg:
   "OpenTelemetry.Instrumentation.StackExchangeRedis") and *not* the
   instrumented library (eg: "StackExchange.Redis")

3. Third step is an optional step, and involves providing extension methods on
   `TracerProviderBuilder`, to enable the instrumentation. This is optional, and
   the below guidance must be followed:

    1. If the instrumentation library requires state management tied to that of
       `TracerProvider`, then it must register itself with the provider with the
       `AddInstrumentation` method on the `TracerProviderBuilder`. This causes
       the instrumentation to be created and disposed along with
       `TracerProvider`. If the above is required, then it must provide an
       extension method on `TracerProviderBuilder`. Inside this extension
       method, it should call the `AddInstrumentation` method, and `AddSource`
       method to enable its ActivitySource for the provider. An example
       instrumentation using this approach is [StackExchangeRedis
       instrumentation](../../../src/OpenTelemetry.Instrumentation.StackExchangeRedis/TracerProviderBuilderExtensions.cs)

    2. If the instrumentation library does not requires any state management
       tied to that of `TracerProvider`, then providing `TracerProviderBuilder`
       extension method is optional. If provided, then it must call `AddSource`
       to enable its ActivitySource for the provider.

    3. If instrumentation library does not require state management, and is not
       providing extension method, then the name of the `ActivitySource` used by
       the instrumented library must be documented so that end users can enable
       it using `AddSource` method on `TracerProviderBuilder`.

There is a special case for libraries which are already instrumented with
[Activity](https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md),
but using the
[DiagnosticSource](https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md)
method. These libraries already emit activities, but it may not conform to the
OpenTelemetry semantic conventions. Also, as these libraries do not use
ActivitySource to create Activity, they cannot be simply subscribed to. In such
cases, the instrumentation library should subscribe to the DiagnosticSource
events from the instrumented library, and in turn produce *new* activity using
ActivitySource. This new activity must be created as a sibling of the activity
already produced by the library. i.e the new activity must have the same parent
as the original activity. Some common examples of such libraries include
Asp.Net, Asp.Net Core, HttpClient (.NET Core). Instrumentation libraries for
these are already provided in this repo.

## Processor

OpenTelemetry .NET SDK has provided the following built-in processors:

* [BatchExportProcessor&lt;T&gt;](../../../src/OpenTelemetry/BatchExportProcessor.cs)
* [CompositeProcessor&lt;T&gt;](../../../src/OpenTelemetry/CompositeProcessor.cs)
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

### Filtering Processor

A common use case of writing custom processor is to filter Activities from being
exported. Such a "FilteringProcessor" can be written as a wrapper around an
underlying processor. An example "FilteringProcessor" is shown
[here](./MyFilteringProcessor.cs).

When using such a filtering processor, instead of using extension method to
register the exporter, they must be registered manually as shown below:

```csharp
    using var tracerProvider = Sdk.CreateTracerProviderBuilder()
        .SetSampler(new MySampler())
        .AddSource("OTel.Demo")
        .AddProcessor(new MyFilteringProcessor(
            new SimpleActivityExportProcessor(new MyExporter("ExporterX")),
            (act) => true))
        .Build();
```

Most [instrumentation libraries](#instrumentation-library) shipped from this
repo provides a built-in `Filter` option to achieve the same effect. In such
cases, it is recommended to use that option as it offers higher performance.

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
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-exporter)
* [Processor
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-processor)
* [Sampler
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampler)
