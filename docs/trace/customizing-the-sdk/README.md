# Customizing OpenTelemetry .NET SDK

## TracerProvider

As shown in the [getting-started](../getting-started/README.md) doc, a valid
[`TracerProvider`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#tracer-provider)
must be configured and built to collect traces with OpenTelemetry .NET Sdk.
`TracerProvider` holds all the configuration for tracing like samplers,
processors, etc. Naturally, almost all the customizations must be done on the
`TracerProvider`.

## Building a TracerProvider

Building a `TracerProvider` is done using `TracerProviderBuilder` which must be
obtained by calling `Sdk.CreateTracerProviderBuilder()`. `TracerProviderBuilder`
exposes various methods which configures the provider it is going to build.
These includes methods like `SetSampler`, `AddProcessor` etc, and are explained
in subsequent sections of this document. Once configuration is done, calling
`Build()` on the `TracerProviderBuilder` builds the `TracerProvider` instance.
Once built, changes to its configuration is not allowed, with the exception of
adding more processors. In most cases, a single `TracerProvider` is created at
the application startup, and is disposed when application shuts down.

The snippet below shows how to build a basic `TracerProvider` and dispose it at
the end of the application. This will create a provider with default
configuration, and is not particularly useful. The subsequent sections shows how
to build a more useful provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();
// ....

// Dispose at application shutdown
tracerProvider.Dispose()

```

In a typical application, a single `TracerProvider` is created at application
startup and disposed at application shutdown. It is important to ensure that the
provider is not disposed too early. Actual mechanism depends on the application
type. For example, in a typical ASP.NET application, `TracerProvider` is created
in `Application_Start`, and disposed in `Application_End` (both methods are a
part of the Global.asax.cs file) as shown
[here](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/examples/AspNet/Global.asax.cs).
In a typical ASP.NET Core application, `TracerProvider` lifetime is managed by
leveraging the built-in Dependency Injection container as shown
[here](../../../examples/AspNetCore/Program.cs).

## TracerProvider configuration

`TracerProvider` holds the tracing configuration, which includes the following:

1. The list of `ActivitySource`s (aka Tracers) from which traces are collected.
2. The list of instrumentations enabled via
   [InstrumentationLibrary](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library).
3. The list of
   [Processors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-processor),
   including exporting processors which exports traces to
   [Exporters](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-exporter)
4. The
   [Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
   associated with the traces.
5. The
   [Sampler](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampler)
   to be used.

### Activity Source

`ActivitySource` denotes a
[`Tracer`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracer),
which is used to create activities. The SDK follows an explicit opt-in model for
listening to activity sources. i.e, by default, it listens to no sources. Every
activity source which produce telemetry must be explicitly added to the tracer
provider to start collecting traces from them.

`AddSource` method on `TracerProviderBuilder` can be used to add a
`ActivitySource` to the provider. The name of the `ActivitySource`
(case-insensitive) must be the argument to this method. Multiple `AddSource` can
be called to add more than one source. It also supports wildcard subscription
model as well.

It is not possible to add sources *after* the provider is built, by calling the
`Build()` method on the `TracerProviderBuilder`.

The snippet below shows how to add activity sources to the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    // The following subscribes to activities from Activity Source
    // named "MyCompany.MyProduct.MyLibrary" only.
    .AddSource("MyCompany.MyProduct.MyLibrary")
    // The following subscribes to activities from all Activity Sources
    // whose name starts with  "AbcCompany.XyzProduct.".
    .AddSource("AbcCompany.XyzProduct.*")
    .Build();
```

See [Program.cs](./Program.cs) for complete example.

**Note** A common mistake while configuring `TracerProvider` is forgetting to
add all `ActivitySources` to the provider. It is recommended to leverage the
wild card subscription model where it makes sense. For example, if your
application is expecting to enable tracing from a number of libraries from a
company "Abc", the you can use `AddSource("Abc.*")` to enable all sources whose
name starts with "Abc.".

### Instrumentation

While the OpenTelemetry API can be used to instrument any library manually,
[Instrumentation
Libraries](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/overview.md#instrumentation-libraries)
are available for a lot of commonly used libraries. Such instrumentations can be
added to the `TracerProvider`. It is not required to attach the instrumentation
to the provider, unless the life cycle of the instrumentation must be managed by
the provider. If the instrumentation must be activated/shutdown/disposed along
with the provider, then the instrumentation must be added to the provider.

Typically, the instrumentation libraries provide extension methods on
`TracerProviderBuilder` to allow adding them to the `TracerProvider`. Please
refer to corresponding documentation of the instrumentation library to know the
exact method name.

Follow [this](../extending-the-sdk/README.md#instrumentation-library) document
to learn about the instrumentation libraries shipped from this repo.

### Processor

[Processors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-processor)
allows hooks for start and end of `Activity`. If no processors are configured,
then traces are simply dropped by the SDK. `AddProcessor` method on
`TracerProviderBuilder` should be used to add a processor. There can be any
number of processors added to the provider, and they are invoked in the same
order as they are added. Unlike `Sampler` or `Resource`, processors can be added
to the provider even *after* it is built.

The snippet below shows how to add processors to the provider before and after
it is built.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddProcessor(new MyProcessor1())
    .AddProcessor(new MyProcessor2()))
    .Build();

// Processors can be added to provider even after it is built.
// Only those traces which are emitted after this line, will be sent to it.
tracerProvider.AddProcessor(new MyProcessor3());
```

A `TracerProvider` assumes ownership of any processors added to it. This means
that, provider will call `Shutdown` method on the processor, when it is
shutdown, and disposes the processor when it is disposed. If multiple providers
are being setup in an application, then separate instances of processors must be
configured on them. Otherwise, shutting down one provider can cause the
processor in other provider to be shut down as well, leading to undesired
results.

Processors can be used for enriching the telemetry and exporting the telemetry
to an exporter. For enriching purposes, one must write a custom processor, and
override the `OnStart` or `OnEnd` method with logic to enrich the telemetry. For
exporting purposes, the SDK provides the following built-in processors:

* [BatchExportProcessor&lt;T&gt;](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#batching-processor)
  : This is an exporting processor which batches the telemetry before sending to
  the configured exporter.

  The following environment variables can be used to override the default
  values of the `BatchExportActivityProcessorOptions`.

  | Environment variable             | `BatchExportActivityProcessorOptions` property |
  | -------------------------------- | ---------------------------------------------- |
  | `OTEL_BSP_SCHEDULE_DELAY`        | `ScheduledDelayMilliseconds`                   |
  | `OTEL_BSP_EXPORT_TIMEOUT`        | `ExporterTimeoutMilliseconds`                  |
  | `OTEL_BSP_MAX_QUEUE_SIZE`        | `MaxQueueSize`                                 |
  | `OTEL_BSP_MAX_EXPORT_BATCH_SIZE` | `MaxExportBatchSizeEnvVarKey`                  |

  `FormatException` is thrown in case of an invalid value for any of the
  supported environment variables.

* [CompositeProcessor&lt;T&gt;](../../../src/OpenTelemetry/CompositeProcessor.cs)
  : This is a processor which can be composed from multiple processors. This is
  typically used to construct multiple processing pipelines, each ending with
  its own exporter.

* [SimpleExportProcessor&lt;T&gt;](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#simple-processor)
  : This is an exporting processor which passes telemetry to the configured
  exporter without any batching.

Follow [this](../extending-the-sdk/README.md#processor) document
to learn about how to write own processors.

*The processors shipped from this SDK are generics, and supports tracing and
logging, by supporting `Activity` and `LogRecord` respectively.*

### Resource

[Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
is the immutable representation of the entity producing the telemetry. If no
`Resource` is explicitly configured, the
[default](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/semantic_conventions/README.md#semantic-attributes-with-sdk-provided-default-value)
resource is used to indicate the
[Service](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/semantic_conventions/README.md#service).
The `ConfigureResource` method on `TracerProviderBuilder` can be used to
configure the resource on the provider. `ConfigureResource` accepts an `Action`
to configure the `ResourceBuilder`. Multiple calls to `ConfigureResource` can be
made. When the provider is built, it builds the final `Resource` combining all
the `ConfigureResource` calls. There can only be a single `Resource` associated
with a provider. It is not possible to change the resource builder *after* the
provider is built, by calling the `Build()` method on the
`TracerProviderBuilder`.

`ResourceBuilder` offers various methods to construct resource comprising of
multiple attributes from various sources. Examples include `AddTelemetrySdk()`
which adds [Telemetry
Sdk](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/semantic_conventions/README.md#telemetry-sdk)
resource, and `AddService()` which adds
[Service](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/semantic_conventions/README.md#service)
resource. It also allows adding `ResourceDetector`s.

Follow [this](../extending-the-sdk/README.md#resource-detector) document
to learn about how to write own resource detectors.

The snippet below shows configuring the `Resource` associated with the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(resourceBuilder => resourceBuilder.AddTelemetrySdk())
    .ConfigureResource(resourceBuilder => resourceBuilder.AddService("service-name"))
    .Build();
```

It is also possible to configure the `Resource` by using following
environmental variables:

| Environment variable       | Description                                        |
| -------------------------- | -------------------------------------------------- |
| `OTEL_RESOURCE_ATTRIBUTES` | Key-value pairs to be used as resource attributes. See the [Resource SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable) for more details. |
| `OTEL_SERVICE_NAME`        | Sets the value of the `service.name` resource attribute. If `service.name` is also provided in `OTEL_RESOURCE_ATTRIBUTES`, then `OTEL_SERVICE_NAME` takes precedence. |

### Sampler

[Samplers](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampler)
are used to control the noise and overhead introduced by OpenTelemetry by
reducing the number of samples of traces collected and sent to the processors.
If no sampler is explicitly configured, the default is to use
`ParentBased(root=AlwaysOn)`. `SetSampler` method on `TracerProviderBuilder` can
be used to set sampler. Only one sampler can be associated with a provider. If
multiple `SetSampler` is called, the last one wins. Also, it is not possible to
change the sampler *after* the provider is built, by calling the `Build()`
method on the `TracerProviderBuilder`.

The snippet below shows configuring a custom sampler to the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetSampler(new TraceIdRatioBasedSampler(0.25))
    .Build();
```

Follow [this](../extending-the-sdk/README.md#sampler) document
to learn about how to write own samplers.

## Context Propagation

// TODO: OpenTelemetry Sdk contents about Context. // TODO: Links to built-in
instrumentations doing Propagation.

## Configuration using Dependency Injection

// TODO: Placeholder
