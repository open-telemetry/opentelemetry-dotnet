# OpenTelemetry .NET SDK

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.svg)](https://www.nuget.org/packages/OpenTelemetry)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.svg)](https://www.nuget.org/packages/OpenTelemetry)

* [Installation](#installation)
* [Introduction](#introduction)
* [Getting started with Logging](#getting-started-with-logging)
* [Getting started with Tracing](#getting-started-with-tracing)
* [Tracing configuration](#tracing-configuration)
  * [Activity Source](#activity-source)
  * [Instrumentation](#instrumentation)
  * [Processor](#processor)
  * [Resource](#resource)
  * [Sampler](#sampler)
* [Advanced topics](#advanced-topics)
  * [Propagators](#propagators)
* [Troubleshooting](#troubleshooting)
  * [Configuration Parameters](#configuration-parameters)
  * [Remarks](#remarks)
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
This SDK also supports [ILogger](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger)
integration.

The SDK deals with concerns such as sampling, processing pipeline, exporting
telemetry to a particular backend etc. In most cases, users indirectly install
and enable the SDK, when they install a particular exporter.

## Getting started with Logging

If you are new to logging, it is recommended to follow [get started in 5
minutes](../../docs/logs/getting-started/README.md) to get up and running with
logging integration with
[`ILogger`](https://docs.microsoft.com/dotnet/api/microsoft.extensions.logging.ilogger).

While [OpenTelemetry
logging](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/overview.md)
specification is an experimental signal, `ILogger` is the de-facto logging API
provided by the .NET runtime and is a stable API recommended for production use.
This repo ships an OpenTelemetry
[provider](https://docs.microsoft.com/dotnet/core/extensions/logging-providers),
which provides the ability to enrich logs emitted with `ILogger` with
`ActivityContext`, and export them to multiple destinations, similar to tracing.
`ILogger` based API will become the OpenTelemetry .NET implementation of
OpenTelemetry logging

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
    // whose name starts with "ABCCompany.XYZProduct.".
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

All the components shipped from this repo uses
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
for its internal logging. The name of the `EventSource` used by OpenTelemetry
SDK is "OpenTelemetry-Sdk". To know the `EventSource` names used by other
components, refer to the individual readme files.

While it is possible to view these logs using tools such as
[PerfView](https://github.com/microsoft/perfview),
[dotnet-trace](https://docs.microsoft.com/dotnet/core/diagnostics/dotnet-trace)
etc., this SDK also ships a [self-diagnostics](#self-diagnostics) feature, which
helps troubleshooting.

## Self-diagnostics

OpenTelemetry SDK ships with built-in self-diagnostics feature. This feature,
when enabled, will listen to internal logs generated by all OpenTelemetry
components (i.e EventSources whose name starts with "OpenTelemetry-") and writes
them to a log file.

The self-diagnostics feature can be enabled/changed/disabled while the process
is running (without restarting the process). The SDK will attempt to read the
configuration file every `10` seconds in non-exclusive read-only mode. The SDK
will create or overwrite a file with new logs according to the configuration.
This file will not exceed the configured max size and will be overwritten in a
circular way.

To enable self-diagnostics, go to the
[current working directory](https://en.wikipedia.org/wiki/Working_directory) of
your process and create a configuration file named `OTEL_DIAGNOSTICS.json` with
the following content:

```json
{
    "LogDirectory": ".",
    "FileSize": 1024,
    "LogLevel": "Error"
}
```

To disable self-diagnostics, delete the above file.

Tip: In most cases, you could just drop the file along your application.
On Windows, you can use [Process Explorer](https://docs.microsoft.com/sysinternals/downloads/process-explorer),
double click on the process to pop up Properties dialog and find "Current
directory" in "Image" tab.
Internally, it looks for the configuration file located in
[GetCurrentDirectory](https://docs.microsoft.com/dotnet/api/system.io.directory.getcurrentdirectory),
and then [AppContext.BaseDirectory](https://docs.microsoft.com/dotnet/api/system.appcontext.basedirectory).
You can also find the exact directory by calling these methods from your code.

### Configuration Parameters

1. `LogDirectory` is the directory where the output log file will be stored. It
   can be an absolute path or a relative path to the current directory.

2. `FileSize` is a positive integer, which specifies the log file size in
   [KiB](https://en.wikipedia.org/wiki/Kibibyte). This value must be between 1 MiB
   and 128 MiB (inclusive), or it will be rounded to the closest upper or lower
   limit. The log file will never exceed this configured size, and will be
   overwritten in a circular way.

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

If the SDK fails to parse the `LogDirectory`, `FileSize` or `LogLevel` fields as
the specified format, the configuration file will be treated as invalid and no
log file would be generated.

When the `LogDirectory` or `FileSize` is found to be changed, the SDK will create
or overwrite a file with new logs according to the new configuration. The
configuration file has to be no more than 4 KiB. In case the file is larger than
4 KiB, only the first 4 KiB of content will be read.

The log file might not be a proper text file format to achieve the goal of having
minimal overhead and bounded resource usage: it may have trailing `NUL`s if log
text is less than configured size; once write operation reaches the end, it will
start from beginning and overwrite existing text.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [OpenTelemetry Tracing SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md)
* [OpenTelemetry Logging specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/overview.md)
