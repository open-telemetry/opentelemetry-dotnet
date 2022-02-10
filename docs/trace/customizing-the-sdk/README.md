# Customizing OpenTelemetry .NET SDK

**This doc is work-in-progress.**

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
exposes various methods which configures the provider it is going to build. These
includes methods like `SetSampler`, `AddProcessor` etc, and are explained in
subsequent sections of this document. Once configuration is done, calling
`Build()` on the `TracerProviderBuilder` builds the `TracerProvider` instance.
Once built, changes to its configuration is not allowed, with the exception of
adding more processors. In most cases, a single `TracerProvider` is created at
the application startup, and is disposed when application shuts down.

The snippet below shows how to build a basic `TracerProvider`. This will create
a provider with default configuration, and is not particularly useful. The
subsequent sections shows how to build a more useful provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();
```

In a typical application, a single `TracerProvider` is created at application
startup and disposed at application shutdown. It is important to ensure that the
provider is not disposed too early. Actual mechanism depends on the application
type. For example, in a typical ASP.NET application, `TracerProvider` is created
in `Application_Start`, and disposed in `Application_End` (both methods part of
Global.asax.cs file) as shown [here](../../../examples/AspNet/Global.asax.cs). In
a typical ASP.NET Core application, `TracerProvider` lifetime is managed by
leveraging the built-in Dependency Injection container as shown
[here](../../../examples/AspNetCore/Startup.cs).

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
which is used to start activities. The SDK follows an explicit opt-in model for
listening to activity sources. i.e, by default, it listens to no sources. Every
activity source which produce telemetry must be explicitly added to the tracer
provider to start collecting traces from them.

`AddSource` method on `TracerProviderBuilder` can be used to add a
`ActivitySource` to the provider. The name of the `ActivitySource`
(case-insensitive) must be the argument to this method. Multiple `AddSource` can
be called to add more than one source. It also supports wild-card subscription
model as well.

It is not possible to add sources *after* the provider is built, by calling the
`Build()` method on the `TracerProviderBuilder`.

The snippet below shows how to add activity sources to the provider.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    // The following subscribes to activities from Activity Source
    // named "MyCompany.MyProduct.MyLibrary" only.
    .AddSource("MyCompany.MyProduct.MyLibrary")
    // The following subscribes to activities from all Activity Sources
    // whose name starts with  "AbcCompany.XyzProduct.".
    .AddSource("AbcCompany.XyzProduct.*")
    .Build();
```

See [Program.cs](./Program.cs) for complete example.

**Note**
A common mistake while configuring `TracerProvider` is forgetting to add
all `ActivitySources` to the provider. It is recommended to leverage the
wild card subscription model where it makes sense. For example, if your
application is expecting to enable tracing from a number of libraries
from a company "Abc", the you can use `AddSource("Abc.*")` to enable
all sources whose name starts with "Abc.".

### Instrumentation

// TODO

### Processor

// TODO

### Resource

// TODO

### Sampler

// TODO

## Context Propagation

// TODO: OpenTelemetry Sdk contents about Context.
// TODO: Links to built-in instrumentations doing Propagation.
