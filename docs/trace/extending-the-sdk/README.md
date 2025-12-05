# Extending the OpenTelemetry .NET SDK

Quick links:

* [Building your own exporter](#exporter)
* [Building your own instrumentation library](#instrumentation-library)
* [Building your own processor](#processor)
* [Building your own sampler](#sampler)
* [Building your own resource detector](../../resources/README.md#resource-detector)
* [Registration extension method guidance for library authors](#registration-extension-method-guidance-for-library-authors)
* [References](#references)

## Exporter

OpenTelemetry .NET SDK has provided the following built-in trace exporters:

* [Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
* [InMemory](../../../src/OpenTelemetry.Exporter.InMemory/README.md)
* [OpenTelemetryProtocol](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
* [Zipkin](../../../src/OpenTelemetry.Exporter.Zipkin/README.md) (Deprecated)

Custom exporters can be implemented to send telemetry data to places which are
not covered by the built-in exporters:

* Exporters should derive from `OpenTelemetry.BaseExporter<Activity>` (which
  belongs to the [OpenTelemetry](../../../src/OpenTelemetry/README.md) package)
  and implement the `Export` method.
* Exporters can optionally implement the `OnForceFlush` and `OnShutdown` method.
* Depending on user's choice and load on the application, `Export` may get
  called with one or more activities.
* Exporters will only receive sampled-in and ended activities.
* Exporters should not throw exceptions from `Export`, `OnForceFlush` and
  `OnShutdown`.
* Exporters should not modify activities they receive (the same activity may be
  exported again by different exporter).
* Exporters are responsible for any retry logic needed by the scenario. The SDK
  does not implement any retry logic.
* Exporters should avoid generating telemetry and causing live-loop, this can be
  done via `OpenTelemetry.SuppressInstrumentationScope`.
* Exporters should use `Activity.TagObjects` collection instead of
  `Activity.Tags` to obtain the full set of attributes (tags). `Activity.Tags` only
   returns tags whose value are of type `string` ([source](https://source.dot.net/#System.Diagnostics.DiagnosticSource/System/Diagnostics/Activity.cs,74de547549e574e0,references)).
  For improved performance, use [Activity.EnumerateTagObjects](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity.enumeratetagobjects?view=net-8.0)
  if planning to enumerate over all TagObjects.
* Exporters should use `ParentProvider.GetResource()` to get the `Resource`
  associated with the provider.

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
shown [here](./MyExporterExtensions.cs). This allows users to add the Exporter
to the `TracerProvider` as shown in the example [here](./Program.cs). See
[here](#registration-extension-method-guidance-for-library-authors) for more
detailed extension method guidance.

### Exporting Activity Status

[DiagnosticSource](https://www.nuget.org/packages/system.diagnostics.diagnosticsource)
package did not originally have a dedicated field for storing
[Status](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#set-status),
and hence, users were encouraged to follow the convention of storing status
using tags "otel.status_code" and "otel.status_description".
[DiagnosticSource](https://www.nuget.org/packages/system.diagnostics.diagnosticsource)
version 6.0.0 added `Status` and `StatusDescription` to `Activity` class.
Exporters which support reading status from `Activity` directly should fall back
to retrieving status from the tags described above, to maintain backward
compatibility.
[ConsoleActivityExporter](../../../src/OpenTelemetry.Exporter.Console/ConsoleActivityExporter.cs)
may be used as a reference.

## Instrumentation Library

The [inspiration of the OpenTelemetry
project](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/overview.md#instrumentation-libraries)
is to make every library observable out of the box by having them call
OpenTelemetry API directly. However, many libraries will not have such
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

* [ASP.NET
  Core](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md)

More community contributed instrumentations are available in [OpenTelemetry .NET
Contrib](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src).
If you are writing an instrumentation library yourself, use the following
guidelines.

### Writing a custom instrumentation library

This section describes the steps required to write a custom instrumentation
library.

> [!NOTE]
> If you are writing a new library or modifying an existing library the
recommendation is to use the [ActivitySource API/OpenTelemetry
API](../../../src/OpenTelemetry.Api/README.md#introduction-to-opentelemetry-net-tracing-api)
to emit activity/span instances directly. If a library is instrumented using the
`ActivitySource` API then there isn't a need for a separate instrumentation
library to exist. Users simply need to configure the OpenTelemetry SDK to listen
to the `ActivitySource` used by the library by calling `AddSource` on the
`TracerProviderBuilder` being configured. The following section is applicable
only if you are writing an instrumentation library for something you cannot
modify to emit activity/span instances directly.

Writing an instrumentation library typically involves 3 steps.

1. The first step involves attaching to the target library. The exact attachment
   mechanism will depend on the implementation details of the target library
   itself. For example, System.Data.SqlClient when running on .NET Framework
   happens to publish events using an `EventSource` which the [SqlClient
   instrumentation
   library](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.SqlClient/Implementation/SqlEventSourceListener.netfx.cs)
   listens to in order to trigger code as Sql commands are executed. The [.NET
   Framework HttpWebRequest
   instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.Http/Implementation/HttpWebRequestActivitySource.netfx.cs)
   patches the runtime code (using reflection) and swaps a static reference that
   gets invoked as requests are processed for custom code. Every library will be
   different.

2. The second step is to emit activity instances using the [ActivitySource
   API](../../../src/OpenTelemetry.Api/README.md#introduction-to-opentelemetry-net-tracing-api)
   **on behalf of** the target library. Irrespective of the actual mechanism
   used in first step, this should be uniform across all instrumentation
   libraries. The `ActivitySource` must be created using the name and version of
   the instrumentation library (eg: "OpenTelemetry.Instrumentation.Http") and
   **NOT** the instrumented library (eg: "System.Net.Http")
      1. [Context
      Propagation](../../../src/OpenTelemetry.Api/README.md#context-propagation):
      If your library initiates out of process requests or accepts them, the
      library needs to [inject the
      `PropagationContext`](../../../examples/MicroserviceExample/Utils/Messaging/MessageSender.cs)
      to outgoing requests and [extract the
      context](../../../examples/MicroserviceExample/Utils/Messaging/MessageReceiver.cs)
      and hydrate the Activity/Baggage upon receiving incoming requests. This is
      only required if you're using your own protocol to communicate over the
      wire. (i.e. If you're using an already instrumented HttpClient or
      GrpcClient, this is already provided to you and **do not require**
      injecting/extracting `PropagationContext` explicitly again.)

3. The third step is an optional step, and involves providing extension methods
   on `TracerProviderBuilder` and/or `IServiceCollection` to enable the
   instrumentation. For help in choosing see: [Registration extension method
   guidance for library
   authors](#registration-extension-method-guidance-for-library-authors). This
   is optional, and the below guidance should be followed:

    * If the instrumentation library requires state management tied to that of
       `TracerProvider` then it should:

      * Implement `IDisposable`.

      * Provide an extension method which calls `AddSource` (to enable its
        `ActivitySource`) and `AddInstrumentation` (to enable state management)
        on the `TracerProviderBuilder` being configured.

        An example instrumentation using this approach is [SqlClient
        instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.SqlClient/TracerProviderBuilderExtensions.cs).

      > [!WARNING]
      > The instrumentation libraries requiring state management are
      usually hard to auto-instrument. Therefore, they take the risk of not
      being supported by [OpenTelemetry .NET Automatic
      Instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation).

    * If the instrumentation library does not require any state management, then
      providing an extension method is optional.

      * If an extension is provided it should call `AddSource` on the
        `TracerProviderBuilder` being configured to enable its
        `ActivitySource`.

      * If an extension is not provided, then the name of the `ActivitySource`
        used by the instrumented library must be documented so that end users
        can enable it by calling `AddSource` on the `TracerProviderBuilder`
        being configured.

        > [!NOTE]
        > Changing the name of the source should be considered a
        breaking change.

### Special case : Instrumentation for libraries producing legacy Activity

There is a special case for libraries which are already instrumented to produce
[Activity](https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.DiagnosticSource/src/ActivityUserGuide.md),
but using the
[DiagnosticSource](https://github.com/dotnet/runtime/blob/master/src/libraries/System.Diagnostics.DiagnosticSource/src/DiagnosticSourceUsersGuide.md)
method. These are referred to as "legacy Activity" in this repo. These libraries
already create activities but they do so by using the `Activity` constructor
directly, rather than using `ActivitySource.StartActivity` method. These
activities does not by default runs through the sampler, and will have their
`Kind` set to internal and they'll have empty ActivitySource name associated
with it.

Some common examples of such libraries include [ASP.NET
Core](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md).
Instrumentation libraries for these are already provided in this repo. The
[OpenTelemetry .NET
Contrib](https://github.com/open-telemetry/opentelemetry-dotnet-contrib)
repository also has instrumentations for libraries like
[ElasticSearchClient](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.ElasticsearchClient)
and [HTTP client .NET Core](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Instrumentation.Http/README.md)
etc. which fall in this category.

If you are writing instrumentation for such library, it is recommended to refer
to one of the above as a reference.

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
* Processors should use `Activity.TagObjects` collection instead of
  `Activity.Tags` to obtain the full set of attributes (tags). `Activity.Tags` only
   returns tags whose value are of type `string` ([source](https://source.dot.net/#System.Diagnostics.DiagnosticSource/System/Diagnostics/Activity.cs,74de547549e574e0,references)).
  For improved performance, use [Activity.EnumerateTagObjects](https://learn.microsoft.com/en-us/dotnet/api/system.diagnostics.activity.enumeratetagobjects?view=net-8.0)
  if planning to enumerate over all TagObjects.

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

### Enriching Processor

A common use case of writing custom processor is to enrich activities with
additional tags. An example of such an "EnrichingProcessor" is shown
[here](./MyEnrichingProcessor.cs). Such processors must be added *before* the
exporters.

This processor also shows how to enrich `Activity` with additional tags from the
`Baggage`.

Many [instrumentation libraries](#instrumentation-library) shipped from this
repo provides a built-in `Enrich` option, which may also be used to enrich
activities. Instrumentation library provided approach may offer additional
capabilities such as offering easy access to more context (library specific).

### Filtering Processor

Another common use case of writing custom processor is to filter Activities from
being exported. Such a "FilteringProcessor" can be written to toggle the
`Activity.Recorded` flag. An example "FilteringProcessor" is shown
[here](./MyFilteringProcessor.cs).

When using such a filtering processor it should be registered BEFORE the
processor containing the exporter which should be bypassed:

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetSampler(new MySampler())
    .AddSource("OTel.Demo")
    .AddProcessor(new MyFilteringProcessor(activity => true))
    .AddProcessor(new SimpleActivityExportProcessor(new MyExporter("ExporterX")))
    .Build();
```

Most [instrumentation libraries](#instrumentation-library) shipped from this
repo provides a built-in `Filter` option to achieve the same effect. In such
cases, it is recommended to use that option as it offers higher performance.

## Sampler

OpenTelemetry .NET SDK has provided the following built-in samplers:

* [AlwaysOffSampler](../../../src/OpenTelemetry/Trace/Sampler/AlwaysOffSampler.cs)
* [AlwaysOnSampler](../../../src/OpenTelemetry/Trace/Sampler/AlwaysOnSampler.cs)
* [ParentBasedSampler](../../../src/OpenTelemetry/Trace/Sampler/ParentBasedSampler.cs)
* [TraceIdRatioBasedSampler](../../../src/OpenTelemetry/Trace/Sampler/TraceIdRatioBasedSampler.cs)

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
        return new SamplingResult(SamplingDecision.RecordAndSample);
    }
}
```

A demo sampler is shown [here](./MySampler.cs).

## Registration extension method guidance for library authors

> [!NOTE]
> This information applies to the OpenTelemetry SDK version 1.4.0 and
newer only.

Library authors are encouraged to provide extension methods users may call to
register custom OpenTelemetry components into their `TracerProvider`s. These
extension methods can target either the `TracerProviderBuilder` or the
`IServiceCollection` classes. Both of these patterns are described below.

> [!NOTE]
> Libraries providing SDK plugins such as exporters, resource detectors,
and/or samplers should take a dependency on the [OpenTelemetry SDK
package](https://www.nuget.org/packages/opentelemetry). Library authors
providing instrumentation should take a dependency on `OpenTelemetry.Api` or
`OpenTelemetry.Api.ProviderBuilderExtensions` package.
`OpenTelemetry.Api.ProviderBuilderExtensions` exposes interfaces for accessing
the `IServiceCollection` which is a requirement for supporting the [.NET Options
pattern](https://learn.microsoft.com/dotnet/core/extensions/options).

When providing registration extensions:

* **DO** support the [.NET Options
  pattern](https://learn.microsoft.com/dotnet/core/extensions/options) and
  **DO** support [named
  options](https://learn.microsoft.com/dotnet/core/extensions/options#named-options-support-using-iconfigurenamedoptions).
  The Options pattern allows users to bind
  [configuration](https://learn.microsoft.com/dotnet/core/extensions/configuration)
  to options classes and provides extension points for working with instances as
  they are created. Multiple providers may exist in the same application for a
  single configuration and multiple components (for example exporters) may exist
  in the same provider. Named options help users target configuration to
  specific components.

  * Use the
    [Configure](https://learn.microsoft.com/dotnet/api/microsoft.extensions.dependencyinjection.optionsservicecollectionextensions.configure#microsoft-extensions-dependencyinjection-optionsservicecollectionextensions-configure-1(microsoft-extensions-dependencyinjection-iservicecollection-system-string-system-action((-0))))
    extension to register configuration callbacks for a given name.

  * Use the
    [IOptionsMonitor&lt;T&gt;.Get](https://learn.microsoft.com/dotnet/api/microsoft.extensions.options.ioptionsmonitor-1.get)
    method to access options class instances by name.

* **DO** throw exceptions for issues that prevent the component being registered
  from starting. The OpenTelemetry SDK is allowed to crash if it cannot be
  started. It **MUST NOT** crash once running.

> [!NOTE]
> The SDK implementation of `TracerProviderBuilder` ensures that the
[.NET
Configuration](https://learn.microsoft.com/en-us/dotnet/core/extensions/configuration)
engine is always available by creating a root `IConfiguration` from environment
variables if it does not already exist in the `IServiceCollection` containing
the `TracerProvider`. Library authors can rely on `IConfiguration` always being
present in the final `IServiceProvider`.

### TracerProviderBuilder extension methods

When registering pipeline components (for example samplers, exporters, or
resource detectors) it is recommended to use the `TracerProviderBuilder` as the
target type for registration extension methods. These extensions will be highly
discoverable for users interacting with the `TracerProviderBuilder` in their IDE
of choice.

The following example shows how to register a custom exporter with named options
support using a `TracerProviderBuilder` extension.

```csharp
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MyLibrary;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Trace
{
    public static class MyLibraryTracerProviderBuilderRegistrationExtensions
    {
        public static TracerProviderBuilder AddMyLibraryExporter(
            this TracerProviderBuilder builder,
            string? name = null,
            Action<MyExporterOptions>? configureExporterOptions = null,
            Action<BatchExportActivityProcessorOptions>? configureBatchProcessorOptions = null)
        {
            ArgumentNullException.ThrowIfNull(builder);

            // Support named options.
            name ??= Options.DefaultName;

            builder.ConfigureServices(services =>
            {
                if (configureExporterOptions != null)
                {
                    // Support configuration through Options API.
                    services.Configure(name, configureExporterOptions);
                }

                if (configureBatchProcessorOptions != null)
                {
                    // Support configuration through Options API.
                    services.Configure(name, configureBatchProcessorOptions);
                }

                // Register custom service as a singleton.
                services.TryAddSingleton<MyCustomService>();
            });

            builder.AddProcessor(serviceProvider =>
            {
                // Retrieve MyExporterOptions instance using name.
                var exporterOptions = serviceProvider.GetRequiredService<IOptionsMonitor<MyExporterOptions>>().Get(name);

                // Retrieve BatchExportActivityProcessorOptions instance using name.
                var batchOptions = serviceProvider.GetRequiredService<IOptionsMonitor<BatchExportActivityProcessorOptions>>().Get(name);

                // Retrieve MyCustomService singleton.
                var myCustomService = serviceProvider.GetRequiredService<MyCustomService>();

                // Return a batch export processor using MyCustomExporter.
                return new BatchActivityExportProcessor(
                    new MyCustomExporter(exporterOptions, myCustomService),
                    batchOptions.MaxQueueSize,
                    batchOptions.ScheduledDelayMilliseconds,
                    batchOptions.ExporterTimeoutMilliseconds,
                    batchOptions.MaxExportBatchSize);
            });

            // Return builder for call chaining.
            return builder;
        }
    }
}

namespace MyLibrary
{
    // Options class can be bound to IConfiguration or configured by code.
    public class MyExporterOptions
    {
        public Uri? IngestionUri { get; set; }
    }

    internal sealed class MyCustomExporter : BaseExporter<Activity>
    {
        public MyCustomExporter(
            MyExporterOptions options,
            MyCustomService myCustomService)
        {
            // Implementation not shown.
        }

        public override ExportResult Export(in Batch<Activity> batch)
        {
            // Implementation not shown.

            return ExportResult.Success;
        }
    }

    internal sealed class MyCustomService
    {
        // Implementation not shown.
    }
}
```

When providing `TracerProviderBuilder` registration extensions:

* **DO** Use the `OpenTelemetry.Trace` namespace for `TracerProviderBuilder`
  registration extensions to help with discoverability.

* **DO** Return the `TracerProviderBuilder` passed in to support call chaining
  of registration methods.

* **DO** Use the `TracerProviderBuilder.ConfigureServices` extension method to
  register dependent services.

* **DO** Use [the dependency injection extension
  methods](../customizing-the-sdk/README.md#dependency-injection-tracerproviderbuilder-extension-method-reference)
  utilizing factory patterns to perform configuration once the final
  `IServiceProvider` is available.

### IServiceCollection extension methods

When registering instrumentation or listening to telemetry in a library
providing other features it is recommended to use the `IServiceCollection` as
the target type for registration extension methods.

The following example shows how a library might enable tracing and metric
support using an `IServiceCollection` extension by calling
`ConfigureOpenTelemetryTracerProvider` and
`ConfigureOpenTelemetryMeterProvider`.

```csharp
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using MyLibrary;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MyLibraryServiceCollectionRegistrationExtensions
    {
        public static IServiceCollection AddMyLibrary(
            this IServiceCollection services,
            string? name = null,
            Action<MyLibraryOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            // Register library services.
            services.TryAddSingleton<IMyLibraryService, MyLibraryService>();

            // Support named options.
            name ??= Options.Options.DefaultName;

            if (configure != null)
            {
                // Support configuration through Options API.
                services.Configure(name, configure);
            }

            // Configure OpenTelemetry tracing.
            services.ConfigureOpenTelemetryTracerProvider((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<MyLibraryOptions>>().Get(name);
                if (options.EnableTracing)
                {
                    builder.AddSource("MyLibrary");
                }
            });

            // Configure OpenTelemetry metrics.
            services.ConfigureOpenTelemetryMeterProvider((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<MyLibraryOptions>>().Get(name);
                if (options.EnableMetrics)
                {
                    builder.AddMeter("MyLibrary");
                }
            });

            return services;
        }
    }
}

namespace MyLibrary
{
    // Options class can be bound to IConfiguration or configured by code.
    public class MyLibraryOptions
    {
        public bool EnableTracing { get; set; }

        public bool EnableMetrics { get; set; }
    }

    internal sealed class MyLibraryService : IMyLibraryService
    {
        // Implementation not shown.
    }

    public interface IMyLibraryService
    {
        // Implementation not shown.
    }
}
```

The benefit to using the `IServiceCollection` style is users only need to call a
single `AddMyLibrary` extension to configure the library itself and optionally
turn on OpenTelemetry integration for multiple signals (tracing & metrics in
this case).

> [!NOTE]
> `ConfigureOpenTelemetryTracerProvider` and
`ConfigureOpenTelemetryMeterProvider` do not automatically start OpenTelemetry.
The host is responsible for either calling `AddOpenTelemetry` in the
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
package, calling `Build` when using the `Sdk.CreateTracerProviderBuilder` and
`Sdk.CreateMeterProviderBuilder` methods, or by accessing the `TracerProvider`
and `MeterProvider` from the `IServiceCollection` where configuration was
performed.

When providing `IServiceCollection` registration extensions:

* **DO** Use the `Microsoft.Extensions.DependencyInjection` namespace for
  `IServiceCollection` registration extensions to help with discoverability.

* **DO** Return the `IServiceCollection` passed in to support call chaining of
  registration methods.

* **DO** Use the `IServiceCollection` directly to register dependent services.

* **DO** Use [the dependency injection extension
  methods](../customizing-the-sdk/README.md#dependency-injection-tracerproviderbuilder-extension-method-reference)
  utilizing factory patterns to perform configuration once the final
  `IServiceProvider` is available.

## References

* [Exporter
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-exporter)
* [Processor
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-processor)
* [Resource
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
* [Sampler
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampler)
