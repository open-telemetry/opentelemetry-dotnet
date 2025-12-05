# Customizing OpenTelemetry .NET SDK for Tracing

## TracerProvider

As shown in the [Getting Started - ASP.NET Core
Application](../getting-started-aspnetcore/README.md) and [Getting Started -
Console Application](../getting-started-console/README.md) docs, OpenTelemetry
tracing is managed by a
[`TracerProvider`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#tracer-provider)
instance configured using the `TracerProviderBuilder` API.

`TracerProviderBuilder` exposes various methods to configure the provider (ex:
`SetSampler`, `AddProcessor`, etc.) which are explained in subsequent sections
of this document. It is also common for library authors to target
`TracerProviderBuilder` for extension methods which help configure SDK plug-in
components.

## Building a TracerProvider

There are two different ways to create a `TracerProviderBuilder`.

### Using OpenTelemetry.Extensions.Hosting

For [ASP.NET
Core](https://learn.microsoft.com/aspnet/core/fundamentals/host/web-host) and
[.NET Generic](https://learn.microsoft.com/dotnet/core/extensions/generic-host)
host users, helper extensions are provided in the
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
package to simplify configuration and management of the `TracerProvider`.

```csharp
using OpenTelemetry.Trace;

var appBuilder = WebApplication.CreateBuilder(args);

appBuilder.Services.AddOpenTelemetry()
    .WithTracing(builder => builder.AddConsoleExporter());
```

> [!NOTE]
> The
[AddOpenTelemetry](../../../src/OpenTelemetry.Extensions.Hosting/README.md#extension-method-reference)
extension automatically starts and stops the `TracerProvider` with the host.

### Using Sdk.CreateTracerProviderBuilder

`Sdk.CreateTracerProviderBuilder()` is provided on all runtimes to create
`TracerProvider`s when either hosting is not available or multiple providers are
required.

Call `Sdk.CreateTracerProviderBuilder()` to obtain a builder and then call
`Build()` once configuration is done to retrieve the `TracerProvider` instance.

> [!NOTE]
> Once built changes to `TracerProvider` configuration are not allowed,
with the exception of adding more processors.

In most cases a single `TracerProvider` is created at the application startup,
and is disposed when application shuts down.

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

## TracerProvider configuration

`TracerProvider` holds the tracing configuration, which includes the following:

1. The list of `ActivitySource`s (aka `Tracer`s) from which traces are collected.
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

> [!NOTE]
> A common mistake while configuring `TracerProvider` is forgetting to
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
to learn about the instrumentation libraries shipped from this repo and writing
custom instrumentation libraries.

### Processors & Exporters

[Processors](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-processor)
expose hooks for start and end processing of `Activity` instances. If no
processors are configured then traces are simply dropped by the SDK. The
`AddProcessor` method on `TracerProviderBuilder` is provided to add a processor
to the SDK pipeline. There can be any number of processors added to the provider
and they are invoked in the same order as they are added. Unlike `Sampler` and
`Resource`, processors can be added to the provider even *after* it is built.

[Exporters](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#span-exporter)
expose hooks for exporting batches of completed `Activity` instances (a batch
may contain a single or many records) and are called by processors. Two base
processor classes `SimpleExportProcessor` & `BatchExportProcessor` are provided
to support invoking exporters through the processor pipeline and implement the
standard behaviors prescribed by the OpenTelemetry specification.

> [!NOTE]
> The SDK only ever invokes processors and has no direct knowledge of
any registered exporters.

#### Processor Configuration

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

> [!NOTE]
> The order of processor registration is important. Each processor added
is invoked in order by the SDK. For example if a simple exporting processor is
added before an enrichment processor the exported data will not contain anything
added by the enrichment because it happens after the export.
<!-- This comment is to make sure the two notes above and below are not merged -->
> [!NOTE]
> A `TracerProvider` assumes ownership of **all** processors added to
it. This means that the provider will call the `Shutdown` method on all
registered processors when it is shutting down and call the `Dispose` method on
all registered processors when it is disposed. If multiple providers are being
set up in an application then separate instances of processors **MUST** be
registered on each provider. Otherwise shutting down one provider will cause the
shared processor(s) in other providers to be shut down as well which may lead to
undesired results.

Processors can be used for enriching, exporting, and/or filtering telemetry.

To enrich telemetry, users may write custom processors overriding the `OnStart`
and/or `OnEnd` methods (as needed) to implement custom logic to change the data
before it is passed to the next processor in the pipeline.

For exporting purposes, the SDK provides the following built-in processors:

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

* [SimpleExportProcessor&lt;T&gt;](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#simple-processor)
  : This is an exporting processor which passes telemetry to the configured
  exporter immediately without any batching.

> [!NOTE]
> A special processor
[CompositeProcessor&lt;T&gt;](../../../src/OpenTelemetry/CompositeProcessor.cs)
is used by the SDK to chain multiple processors together and may be used as
needed by users to define sub-pipelines.
<!-- This comment is to make sure the two notes above and below are not merged -->
> [!NOTE]
> The processors shipped from this SDK are generic implementations and support
tracing and logging by implementing `Activity` and `LogRecord` respectively.

Follow [this](../extending-the-sdk/README.md#processor) document to learn about
writing custom processors.

#### Exporter Configuration

The snippet below shows how to add export processors to the provider before it
is built.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddProcessor(new BatchActivityExportProcessor(new MyExporter1()))
    .AddProcessor(new SimpleActivityExportProcessor(new MyExporter2()))
    .Build();
```

It is also common for exporters to provide their own extensions to simplify
registration. The snippet below shows how to add the
[OtlpExporter](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
to the provider before it is built.

 ```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddOtlpExporter()
    .Build();
```

Follow [this](../extending-the-sdk/README.md#exporter) document to learn about
writing custom exporters.

### Resource

[Resource](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md)
is the immutable representation of the entity producing the telemetry. If no
`Resource` is explicitly configured, the
[default](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#semantic-attributes-with-sdk-provided-default-value)
is to use a resource indicating this
[Service](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#service)
and [Telemetry
SDK](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#telemetry-sdk).
The `ConfigureResource` method on `TracerProviderBuilder` can be used to
configure the resource on the provider. `ConfigureResource` accepts an `Action`
to configure the `ResourceBuilder`. Multiple calls to `ConfigureResource` can be
made. When the provider is built, it builds the final `Resource` combining all
the `ConfigureResource` calls. There can only be a single `Resource` associated
with a provider. It is not possible to change the resource builder *after* the
provider is built, by calling the `Build()` method on the
`TracerProviderBuilder`.

`ResourceBuilder` offers various methods to construct resource comprising of
attributes from various sources. For example, `AddService()` adds
[Service](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/resource/README.md#service)
resource. `AddAttributes` can be used to add any additional attribute to the
`Resource`. It also allows adding `ResourceDetector`s.

It is recommended to model attributes that are static throughout the lifetime of
the process as Resources, instead of adding them as attributes(tags) on each
`Activity`.

Follow [this](../../resources/README.md#resource-detector) document
to learn about writing custom resource detectors.

The snippet below shows configuring the `Resource` associated with the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureResource(r => r.AddAttributes(new List<KeyValuePair<string, object>>
                {
                    new KeyValuePair<string, object>("static-attribute1", "v1"),
                    new KeyValuePair<string, object>("static-attribute2", "v2"),
                }))
    .ConfigureResource(resourceBuilder => resourceBuilder.AddService("service-name"))
    .Build();
```

It is also possible to configure the `Resource` by using following
environmental variables:

| Environment variable | Description |
| -------------------------- | -------------------------------------------------- |
| `OTEL_RESOURCE_ATTRIBUTES` | Key-value pairs to be used as resource attributes. See the [Resource SDK specification](https://github.com/open-telemetry/opentelemetry-specification/blob/v1.5.0/specification/resource/sdk.md#specifying-resource-information-via-an-environment-variable) for more details. |
| `OTEL_SERVICE_NAME` | Sets the value of the `service.name` resource attribute. If `service.name` is also provided in `OTEL_RESOURCE_ATTRIBUTES`, then `OTEL_SERVICE_NAME` takes precedence. |

### Samplers

[Samplers](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampler)
are used to control the noise and overhead introduced by OpenTelemetry by
reducing the number of samples of traces collected and sent to the processors.
If no sampler is explicitly configured, the default is to use
`ParentBased(root=AlwaysOn)`. `SetSampler` method on `TracerProviderBuilder` can
be used to set sampler. Only one sampler can be associated with a provider. If
`SetSampler` is called multiple times, the last one wins. Also, it is not
possible to change the sampler *after* the provider is built, by calling the
`Build()` method on the `TracerProviderBuilder`.

The snippet below shows configuring a custom sampler to the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .SetSampler(new TraceIdRatioBasedSampler(0.25))
    .Build();
```

If using `1.8.0-rc.1` or newer it is also possible to configure the sampler by
using the following environmental variables:

| Environment variable | Description |
| -------------------------- | -------------------------------------------------- |
| `OTEL_TRACES_SAMPLER` | Sampler to be used for traces. See the [General SDK Configuration specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md#general-sdk-configuration) for more details. |
| `OTEL_TRACES_SAMPLER_ARG` | String value to be used as the sampler argument. |

The supported values for `OTEL_TRACES_SAMPLER` are:

* `always_off`
* `always_on`
* `traceidratio`
* `parentbased_always_on`,
* `parentbased_always_off`
* `parentbased_traceidratio`

The options `traceidratio` and `parentbased_traceidratio` may have the sampler
probability configured via the `OTEL_TRACES_SAMPLER_ARG` environment variable.

Follow [this](../extending-the-sdk/README.md#sampler) document
to learn about writing custom samplers.

## Context Propagation

The OpenTelemetry API exposes a method to obtain the default propagator which is
no-op, by default. This SDK replaces the no-op with a [composite
propagator](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/context/api-propagators.md#composite-propagator)
containing the Baggage Propagator and TraceContext propagator. This default
propagator can be overridden with the below snippet.

```csharp
using OpenTelemetry;

Sdk.SetDefaultTextMapPropagator(new MyCustomPropagator());
```

## Dependency injection support

> [!NOTE]
> This information applies to the OpenTelemetry SDK version 1.4.0 and
newer only.

The SDK implementation of `TracerProviderBuilder` is backed by an
`IServiceCollection` and supports a wide range of APIs to enable what is
generally known as [dependency
injection](https://learn.microsoft.com/dotnet/core/extensions/dependency-injection).

### Dependency injection examples

For the below examples imagine a processor with this constructor:

```csharp
public class MyCustomProcessor : BaseProcessor<Activity>
{
    public MyCustomProcessor(MyCustomService myCustomService)
    {
        // Implementation not important
    }
}
```

We want to inject `MyCustomService` dependency into our `MyCustomProcessor`
instance.

#### Using Sdk.CreateTracerProviderBuilder()

To register `MyCustomProcessor` and `MyCustomService` we can use the
`ConfigureServices` and `AddProcessor` methods:

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .ConfigureServices(services =>
    {
        services.AddSingleton<MyCustomService>();
    })
    .AddProcessor<MyCustomProcessor>()
    .Build();
```

When using the `Sdk.CreateTracerProviderBuilder` method the `TracerProvider`
owns its own `IServiceCollection`. It will only be able to see services
registered into that collection.

> [!NOTE]
> It is important to correctly manage the lifecycle of the
`TracerProvider`. See [Building a TracerProvider](#building-a-tracerprovider)
for details.

#### Using the OpenTelemetry.Extensions.Hosting package

> [!NOTE]
> If you are authoring an [ASP.NET Core
application](https://learn.microsoft.com/aspnet/core/fundamentals/host/web-host)
or using the [.NET Generic
Host](https://learn.microsoft.com/dotnet/core/extensions/generic-host) the
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
package is the recommended mechanism.

```csharp
var appBuilder = WebApplication.CreateBuilder(args);

appBuilder.Services.AddSingleton<MyCustomService>();

appBuilder.Services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddProcessor<MyCustomProcessor>());
```

When using the `AddOpenTelemetry` & `WithTracing` extension methods the
`TracerProvider` does not own its `IServiceCollection` and instead registers
into an existing collection (typically the collection used is the one managed by
the application host). The `TracerProviderBuilder` will be able to access all
services registered into that collection. For lifecycle management, the
`AddOpenTelemetry` registers an
[IHostedService](https://learn.microsoft.com/dotnet/api/microsoft.extensions.hosting.ihostedservice)
which is used to automatically start the `TracerProvider` when the host starts
and the host will automatically shutdown and dispose the `TracerProvider` when
it is shutdown.

> [!NOTE]
> Multiple calls to `WithTracing` will configure the same
`TracerProvider`. Only a single `TraceProvider` may exist in an
`IServiceCollection` \ `IServiceProvider`.

### Dependency injection TracerProviderBuilder extension method reference

* `AddInstrumentation<T>`: Adds instrumentation of type `T` into the
  `TracerProvider`.

* `AddInstrumentation<T>(Func<IServiceProvider, T> instrumentationFactory)`:
  Adds instrumentation of type `T` into the
  `TracerProvider` using a factory function to create the instrumentation
  instance.

* `AddProcessor<T>`: Adds a processor of type `T` (must derive from
  `BaseProcessor<Activity>`) into the `TracerProvider`.

* `AddProcessor(Func<IServiceProvider, BaseProcessor<Activity>>
  implementationFactory)`: Adds a processor into the `TracerProvider` using a
  factory function to create the processor instance.

* `ConfigureServices`: Registers a callback function for configuring the
  `IServiceCollection` used by the `TracerProviderBuilder`.

  > [!NOTE]
  > `ConfigureServices` may only be called before the `IServiceProvider`
  has been created after which point services can no longer be added.

* `SetSampler<T>`: Register type `T` (must derive from `Sampler`) as the sampler
  for the `TracerProvider`.

* `SetSampler(Func<IServiceProvider, Sampler>
  implementationFactory)`: Adds a sampler into the `TracerProvider` using a
  factory function to create the sampler instance.

> [!NOTE]
> The factory functions accepting `IServiceProvider` may always be used
regardless of how the SDK is initialized. When using an external service
collection (ex: `appBuilder.Services.AddOpenTelemetry()`), as is common in
ASP.NET Core hosts, the `IServiceProvider` will be the instance shared and
managed by the host. When using "Sdk.Create" functions, as is common in .NET
Framework hosts, the provider creates its own `IServiceCollection` and will
build an `IServiceProvider` from it to make available to extensions.

## Configuration files and environment variables

> [!NOTE]
> This information applies to the OpenTelemetry SDK version 1.4.0 and
newer only.

The OpenTelemetry .NET SDK integrates with the standard
[configuration](https://learn.microsoft.com/dotnet/core/extensions/configuration)
and [options](https://learn.microsoft.com/dotnet/core/extensions/options)
patterns provided by .NET. The configuration pattern supports building a
composited view of settings from external sources and the options pattern helps
use those settings to configure features by binding to simple classes.

### How to set up configuration

The following sections describe how to set up configuration based on the host
and OpenTelemetry API being used.

#### Using .NET hosts with the OpenTelemetry.Extensions.Hosting package

[ASP.NET
Core](https://learn.microsoft.com/aspnet/core/fundamentals/host/web-host) and
[.NET Generic](https://learn.microsoft.com/dotnet/core/extensions/generic-host)
host users using the
[OpenTelemetry.Extensions.Hosting](../../../src/OpenTelemetry.Extensions.Hosting/README.md)
package do not need to do anything extra to enable `IConfiguration` support. The
OpenTelemetry SDK will automatically use whatever `IConfiguration` has been
supplied by the host. The host by default will load environment variables,
command-line arguments, and config files. See [Configuration in
.NET](https://learn.microsoft.com/dotnet/core/extensions/configuration) for
details.

#### Using Sdk.CreateTracerProviderBuilder directly

By default the `Sdk.CreateTracerProviderBuilder` API will create an
`IConfiguration` from environment variables. The following example shows how to
customize the `IConfiguration` used by `Sdk.CreateTracerProviderBuilder` for
cases where additional sources beyond environment variables are required.

```csharp
// Build configuration from sources. Order is important.
var configuration = new ConfigurationBuilder()
    .AddJsonFile("./myOTelSettings.json")
    .AddEnvironmentVariables()
    .AddCommandLine(args)
    .Build();

// Set up a TracerProvider using the configuration.
var provider = Sdk.CreateTracerProviderBuilder()
    .ConfigureServices(services => services.AddSingleton<IConfiguration>(configuration))
    .Build();
```

### OpenTelemetry Specification environment variables

The [OpenTelemetry
Specification](https://github.com/open-telemetry/opentelemetry-specification)
defines [specific environment
variables](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/configuration/sdk-environment-variables.md)
which may be used to configure SDK implementations.

The OpenTelemetry .NET SDK will look for the environment variables defined in
the specification using `IConfiguration` which means in addition to environment
variables users may also manage these settings via the command-line,
configuration files, or any other source registered with the .NET configuration
engine. This provides greater flexibility than what the specification defines.

> [!NOTE]
> Not all of the environment variables defined in the specification are
supported. Consult the individual project README files for details on specific
environment variable support.

As an example the OpenTelemetry Specification defines the `OTEL_SERVICE_NAME`
environment variable which may be used to configure the service name emitted on
telemetry by the SDK.

A traditional environment variable is set using a command like `set
OTEL_SERVICE_NAME=MyService` on Windows or `export OTEL_SERVICE_NAME=MyService`
on Linux.

That works as expected but the OpenTelemetry .NET SDK is actually looking for
the `OTEL_SERVICE_NAME` key in `IConfiguration` which means it may also be
configured in any configuration source registered with the
`IConfigurationBuilder` used to create the final configuration for the host.

Below are two examples of configuring the `OTEL_SERVICE_NAME` setting beyond
environment variables.

* Using appsettings.json:

  ```json
  {
     "OTEL_SERVICE_NAME": "MyService"
  }
  ```

* Using command-line:

  ```sh
  dotnet run --OTEL_SERVICE_NAME "MyService"
  ```

> [!NOTE]
> The [.NET
  Configuration](https://learn.microsoft.com/dotnet/core/extensions/configuration)
  pattern is hierarchical meaning the order of registered configuration sources
  controls which value will seen by the SDK when it is defined in multiple
  sources.

### Using the .NET Options pattern to configure the SDK

Options are typically simple classes containing only properties with public
"getters" and "setters" (aka POCOs) and have "Options" at the end of the class
name. These options classes are primarily used when interacting with the
`TracerProviderBuilder` to control settings and features of the different SDK
components.

Options classes can always be configured through code but users typically want to
control key settings through configuration.

The following example shows how to configure `OtlpExporterOptions` by binding
to an `IConfiguration` section.

Json config file (usually appsettings.json):

```json
{
  "OpenTelemetry": {
    "Otlp": {
      "Endpoint": "http://localhost:4317"
    }
  }
}
```

Code:

```csharp
var appBuilder = WebApplication.CreateBuilder(args);

appBuilder.Services.Configure<OtlpExporterOptions>(
    appBuilder.Configuration.GetSection("OpenTelemetry:Otlp"));

appBuilder.Services.AddOpenTelemetry()
    .WithTracing(builder => builder.AddOtlpExporter());
```

The OpenTelemetry .NET SDK supports running multiple `TracerProvider`s inside
the same application and it also supports registering multiple similar
components such as exporters into a single `TracerProvider`. In order to allow
users to target configuration at specific components a "name" parameter is
typically supported on configuration extensions to control the options instance
used for the component being registered.

The below example shows how to configure two `OtlpExporter` instances inside a
single `TracerProvider` sending to different ports.

Json config file (usually appsettings.json):

```json
{
  "OpenTelemetry": {
    "OtlpPrimary": {
      "Endpoint": "http://localhost:4317"
    },
    "OtlpSecondary": {
      "Endpoint": "http://localhost:4327"
    },
  }
}
```

Code:

```csharp
var appBuilder = WebApplication.CreateBuilder(args);

appBuilder.Services.Configure<OtlpExporterOptions>(
    "OtlpPrimary",
    appBuilder.Configuration.GetSection("OpenTelemetry:OtlpPrimary"));

appBuilder.Services.Configure<OtlpExporterOptions>(
    "OtlpSecondary",
    appBuilder.Configuration.GetSection("OpenTelemetry:OtlpSecondary"));

appBuilder.Services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddOtlpExporter(name: "OtlpPrimary", configure: null)
        .AddOtlpExporter(name: "OtlpSecondary", configure: null));
```
