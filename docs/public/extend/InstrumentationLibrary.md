
# Instrumentation Library

The
[inspiration of the OpenTelemetry project](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/overview.md#instrumentation-libraries)
is to make every library observable out of the box by having them call
OpenTelemetry API directly. However, many libraries will not have such
integration, and as such there is a need for a separate library which would
inject such calls, using mechanisms such as wrapping interfaces, subscribing to
library-specific callbacks, or translating existing telemetry into
OpenTelemetry model.

A library which enables instrumentation for another library is called
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library)
and the library it instruments is called the
[Instrumented Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumented-library).
If a given library has built-in instrumentation with OpenTelemetry, then
instrumented library and instrumentation library will be the same.

The
[OpenTelemetry .NET Github repo](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/README.md#getting-started)
ships the following instrumentation libraries. The individual docs for them
describes the library they instrument, and steps for enabling them.

- [ASP.NET](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.AspNet/README.md)
- [ASP.NET Core](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
- [gRPC Client](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.GrpcNetClient/README.md)
- [HTTP Client](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.Http/README.md)
- [Redis Client](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.StackExchangeRedis/README.md)
- [SQL Client](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.SqlClient/README.md)

More community contributed instrumentations are available in [OpenTelemetry .NET
Contrib](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src).
If you are writing an instrumentation library yourself, use the following
guidelines.

## Writing own instrumentation library

This section describes the steps required to write your own instrumentation
library.

*If you are writing a new library or modifying an existing library, the
recommendation is to use [ActivitySource API/OpenTelemetry
API](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Api/README.md#introduction-to-opentelemetry-net-tracing-api)
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
   library](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.StackExchangeRedis/README.md)
   in this case, leverages them. Another example is System.Data.SqlClient for
   .NET Framework, which publishes events using `EventSource`. The [SqlClient
   instrumentation
   library](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.SqlClient/Implementation/SqlEventSourceListener.netfx.cs),
   in this case subscribes to the `EventSource` callbacks.

2. Second step is to emit activities using the [ActivitySource
   API](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Api/README.md#introduction-to-opentelemetry-net-tracing-api).
   In this step, the instrumentation library emits activities *on behalf of* the
   target instrumented library. Irrespective of the actual mechanism used in
   first step, this should be uniform across all instrumentation libraries. The
   `ActivitySource` must be created using the name and version of the
   instrumentation library (eg:
   "OpenTelemetry.Instrumentation.StackExchangeRedis") and *not* the
   instrumented library (eg: "StackExchange.Redis")
      1. [Context Propagation](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Api/README.md#context-propagation):
      If your library initiates out of process requests or
      accepts them, the library needs to
      [inject the `PropagationContext`](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/examples/MicroserviceExample/Utils/Messaging/MessageSender.cs)
      to outgoing requests and
      [extract the context](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/examples/MicroserviceExample/Utils/Messaging/MessageReceiver.cs)
      and hydrate the Activity/Baggage upon receiving incoming requests.
      This is only required if you're using your own protocol to
      communicate over the wire.
      (i.e. If you're using an already instrumented HttpClient or GrpcClient,
      this is already provided to you and **do not require**
      injecting/extracting `PropagationContext` explicitly again.)

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
      instrumentation using this approach is
      [StackExchangeRedis Instrumentation](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.StackExchangeRedis/TracerProviderBuilderExtensions.cs)
   2. If the instrumentation library does not requires any state management
      tied to that of `TracerProvider`, then providing `TracerProviderBuilder`
      extension method is optional. If provided, then it must call `AddSource`
      to enable its ActivitySource for the provider.
   3. If instrumentation library does not require state management, and is not
      providing extension method, then the name of the `ActivitySource` used by
      the instrumented library must be documented so that end users can enable
      it using `AddSource` method on `TracerProviderBuilder`.

## Instrumentation for libraries producing legacy Activity

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

Some common examples of such libraries include
[ASP.NET](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.AspNet/README.md)
,
[ASP.NET Core](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.AspNetCore/README.md)
,
[HTTP Client .NET Core](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Instrumentation.Http/README.md)
.
Instrumentation libraries for these are already provided in this repo. The
[OpenTelemetry .NET Contrib](https://github.com/open-telemetry/opentelemetry-dotnet-contrib)
repostory also has instrumentations for libraries like
[ElasticSearchClient](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/tree/main/src/OpenTelemetry.Contrib.Instrumentation.ElasticsearchClient)
etc. which fall in this category.

If you are writing instrumentation for such library, it is recommended to refer
to one of the above as a reference.
