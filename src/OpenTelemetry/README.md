# OpenTelemetry .NET SDK

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.svg)](https://www.nuget.org/packages/OpenTelemetry)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.svg)](https://www.nuget.org/packages/OpenTelemetry)

* [Installation](#installation)
* [Introduction](#introduction)
* [Getting started with Logs](#getting-started-with-logging)
* [Getting started with Traces](#getting-started-with-tracing)
* [Tracing Configuration](#tracing-configuration)
  * [ActivitySource](#activity-source)
  * [Instrumentation](#instrumentation)
  * [Processor](#processor)
  * [Resource](#resource)
  * [Sampler](#sampler)
* [Advanced topics](#advanced-topics)
  * [Propagators](#propagators)
* [Troubleshooting](#troubleshooting)
* [References](#references)

## Installation

```shell
dotnet add package OpenTelemetry
```

## Introduction

OpenTelemetry SDK is a reference implementation of the OpenTelemetry API. It
implements the Tracing API, the Metrics API, and the Context API.  Once a valid
SDK is installed and configured, all the OpenTelemetry API methods, which were
no-ops without an SDK, will start emitting telemetry.
This SDK also supports
[Logging](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/overview.md)
by integrating with
[ILogger](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger).

The SDK deals with concerns such as sampling, processing pipeline, exporting
telemetry to a particular backend etc. In most cases, users indirectly install
and enable the SDK, when they install a particular exporter.

## Getting started with Logging

If you are new to
[logging](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/overview.md),
it is recommended to follow [get started in 5
minutes](../../docs/logs/getting-started/README.md) to get up and running with
logging integration with
[`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger).

## Getting started with Tracing

If you are new to
[traces](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md),
it is recommended to follow [get started in 5
minutes](../../docs/trace/getting-started/README.md) to get up and running. The
rest of this document explains various components of this OpenTelemetry SDK
implementation.

To start using OpenTelemetry for tracing, one must configure and build a valid
[`TracerProvider`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#tracer-provider).

Building a `TracerProvider` is done using `TracerProviderBuilder` which must be
obtained by calling `Sdk.CreateTracerProviderBuilder()`. `TracerProviderBuilder`
exposes various methods which configures the provider it is going to build. This
includes methods like `SetSampler`, `AddProcessor` etc, and are explained in
subsequent sections of this document. Once configuration is done, calling
`Build()` on the `TracerProviderBuilder` builds the `TracerProvider` instance.
Once built, changes to its configuration is not allowed, with the exception of
adding more processors. In most cases, a single `TracerProvider` is created at
the application startup, and is disposed when application shuts down.

// TODO: Add Asp.Net Core, Asp.Net notes showing where this code should go.

The snippet below shows how to build a basic `TracerProvider`. This will create
a provider with default configuration, and is not particularly useful. The
subsequent sections shows how to configure the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();
```

## Tracing configuration

`TracerProvider` holds the SDK configuration. It includes the following:

1. The list of `ActivitySource`s (aka Tracer) from which traces are collected.
2. The list of instrumentations enabled via
   [InstrumentationLibrary](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library).
3. The list of
   [Processors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-processor)

4. The
   [Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
   associated with the traces.
5. The
   [Sampler](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampler)
   to be used.

### Activity Source

`ActivitySource` denotes a
[`Tracer`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracer),
which is used to start activities. The SDK follows an explicit opt-in model for
listening to activity sources. i.e, by default, it listens to no sources. Every
activity source which produce telemetry must be explicitly added to the tracer
provider to start collecting traces from them.

`AddSource` method on `TracerProviderBuilder` can be used to add a
`ActivitySource` to the provider. Multiple `AddSource` can be called to add more
than one source. It also supports wild-card subscription model as well.

Similar to `Sampler` and `Resource`, it is not possible to add sources *after*
the provider is built, by calling the `Build()` method on the
`TracerProviderBuilder`.

The snippet below shows how to add activity sources to the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    // The following subscribes to activities from Activity Source
    // named "MyCompany.MyProduct.MyLibrary" only.
    .AddSource("MyCompany.MyProduct.MyLibrary")
    // The following subscribes to activities from all Activity Sources
    // whose name starts with  "ABCCompany.XYZProduct.".
    .AddSource("ABCCompany.XYZProduct.*")
    .Build();
```

### Instrumentation

While the OpenTelemetry API can be used to instrument any library manually,
[Instrumentation
Libraries](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/overview.md#instrumentation-libraries)
are available for a lot of commonly used libraries. Such instrumentations can be
added the tracer provider, by using the `AddInstrumentation` on the
`TracerProviderBuilder`. It is not required to attach the instrumentation to the
provider, unless the life cycle of the instrumentation must be managed by the
provider. If the instrumentation must be activated/shutdown/disposed along with
the provider, then the instrumentation must be added to the provider.

Follow
[this](../../docs/trace/extending-the-sdk/README.md#instrumentation-library)
document to learn about the instrumentation libraries shipped from this repo,
and also to learn about writing own instrumentations.

### Processor

[Processors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-processor)
allows hooks for start and end of telemetry. If no processors are configured,
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

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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
override the `OnStart` method with logic to enrich the telemetry. For exporting
purposes, the SDK provides the following built-in processors:

* [BatchExportProcessor&lt;T&gt;](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#batching-processor)
  : This is an exporting processor which batches the telemetry before sending to
  the configured exporter.
* [CompositeProcessor&lt;T&gt;](../../src/OpenTelemetry/CompositeProcessor.cs)
  : This is a processor which can be composed from multiple processors. This is
  typically used to construct multiple processing pipelines, each ending with
  its own exporter.
* [SimpleExportProcessor&lt;T&gt;](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#simple-processor)
  : This is an exporting processor which passes telemetry to the configured
  exporter without any batching.

Follow [this](../../docs/trace/extending-the-sdk/README.md#processor) document
to learn about how to write own processors.

*The processors shipped from this SDK are generics, and supports tracing and
logging, by supporting `Activity` and `LogRecord` respectively.*

### Resource

[Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
is the immutable representation of the entity producing the telemetry. If no
`Resource` is explicitly configured, the default is to use a resource indicating
this [Telemetry
SDK](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/resource/semantic_conventions#telemetry-sdk).
`SetResourceBuilder` method on `TracerProviderBuilder` can be used to set a
`ResourceBuilder` on the provider. When the provider is built, it automatically
builds the final `Resource` from the configured `ResourceBuilder`. As with
samplers, there can only be a single `Resource` associated with a provider. If
multiple `SetResourceBuilder` is called, the last one wins. Also, it is not
possible to change the resource builder *after* the provider is built, by
calling the `Build()` method on the `TracerProviderBuilder`. `ResourceBuilder`
offers various methods to construct resource comprising of multiple attributes
from various sources.

The snippet below shows configuring a custom `ResourceBuilder` to the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyServiceName"))
    .Build();
```

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

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetSampler(new TraceIdRatioBasedSampler(0.25))
    .Build();
```

## Advanced topics

* Trace
  * [Building your own
    Exporter](../../docs/trace/extending-the-sdk/README.md#exporter)
  * [Building your own Instrumentation
    Library](../../docs/trace/extending-the-sdk/README.md#instrumentation-library)
  * [Building your own
    Processor](../../docs/trace/extending-the-sdk/README.md#processor)
  * [Building your own
    Sampler](../../docs/trace/extending-the-sdk/README.md#sampler)

### Propagators

The OpenTelemetry API exposes a method to obtain the default propagator which is
no-op, by default. This SDK replaces the no-op with a [composite
propagator](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/context/api-propagators.md#composite-propagator)
containing the Baggage Propagator and TraceContext propagator. This default
propagator can be overridden with the below snippet.

```csharp
using OpenTelemetry;

Sdk.SetDefaultTextMapPropagator(new MyCustomPropagator());
```

## Troubleshooting

All the components shipped from this repo uses `EventSource` for its internal
logging. While it is possible to view these logs using tools such as `PerfView`,
this SDK also ships a "Self diagnostics module", which helps troubleshooting.
When enabled, internal events generated by OpenTelemetry will be written to a
log file.

To enable self diagnostics, go to the current directory of your process and
create a configuration file named `OTEL_DIAGNOSTICS.json` with the following
content:

// TODO: Provide explicit example of current directory for Asp.Net, Asp.Net
Core, Console.

```json
{
    "LogDirectory": ".",
    "FileSize": 1024,
    "LogLevel": "Error"
}
```

### Configuration Parameters

1. `LogDirectory` is the directory where the output log file will be stored. It
   can be an absolute path or a relative path to the current directory.

2. `FileSize` is a positive integer, which specifies the log file size in
   [KiB](https://en.wikipedia.org/wiki/Kibibyte).

3. `LogLevel` is the lowest level of the events to be captured. It has to be one
   of the
   [values](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventlevel#fields)
   of the [`EventLevel`
   enum](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventlevel).
   The level signifies the severity of an event. Lower severity levels encompass
   higher severity levels. For example, `Warning` includes the `Error` and
   `Critical` levels.

### Remarks

A `FileSize`-KiB log file named as `ExecutableName.ProcessId.log` (e.g.
`foobar.exe.12345.log`) will be generated at the specified directory
`LogDirectory`, into which logs are written to.

The SDK will attempt to open the configuration file in non-exclusive read-only
mode, read the file and parse it as the configuration file every 10 seconds. If
the SDK fails to parse the `LogDirectory`, `FileSize` or `LogLevel` fields as
the specified format, the configuration file will be treated as invalid and no
log file would be generated. Otherwise, it will create or overwrite the log file
as described above.

Note that the `FileSize` has to be between 1 MiB and 128 MiB (inclusive), or it
will be rounded to the closest upper or lower limit. When the `LogDirectory` or
`FileSize` is found to be changed, the SDK will create or overwrite a file with
new logs according to the new configuration. The configuration file has to be no
more than 4 KiB. In case the file is larger than 4 KiB, only the first 4 KiB of
content will be read.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenTelemetry Tracing SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md)
* [OpenTelemetry Logging specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/overview.md)
