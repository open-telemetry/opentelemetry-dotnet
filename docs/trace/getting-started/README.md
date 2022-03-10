# Getting Started with OpenTelemetry .NET in 5 Minutes

First, download and install the [.NET
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new console application and run it:

```sh
dotnet new console --output getting-started
cd getting-started
dotnet run
```

You should see the following output:

```text
Hello World!
```

Install the
[OpenTelemetry.Exporter.Console](../../../src/OpenTelemetry.Exporter.Console/README.md)
package:

```sh
dotnet add package OpenTelemetry.Exporter.Console
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs):

Run the application again (using `dotnet run`) and you should see the trace
output from the console.

```text
Activity.Id:          00-8389584945550f40820b96ce1ceb9299-745239d26e408342-01
Activity.DisplayName: SayHello
Activity.Kind:        Internal
Activity.StartTime:   2020-08-12T15:59:10.4461835Z
Activity.Duration:    00:00:00.0066039
Activity.TagObjects:
    foo: 1
    bar: Hello, World!
    baz: [1, 2, 3]
Resource associated with Activity:
    service.name: unknown_service:getting-started
```

Congratulations! You are now collecting traces using OpenTelemetry.

What does the above program do?

The program creates an `ActivitySource` which represents an [OpenTelemetry
Tracer](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracer).
The `ActivitySource` instance is used to start an `Activity` which represents an
[OpenTelemetry
Span](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#span).
An OpenTelemetry
[TracerProvider](#tracerprovider)
is configured to subscribe to the activities from the source
`MyCompany.MyProduct.MyLibrary`, and export it to `ConsoleExporter`.
`ConsoleExporter` simply displays it on the console.

## TracerProvider

As shown in the above program, a valid `TracerProvider` must be configured and
built to collect traces with OpenTelemetry .NET SDK. `TracerProvider` holds all
the configuration for tracing like samplers, processors, etc. and is highly
[customizable](../../../src/OpenTelemetry/README.md#tracing-configuration).

## OpenTelemetry .NET and relation with .NET Activity API

If you tried the above program, you may have already noticed that the terms
`ActivitySource` and `Activity` were used instead of `Tracer` and `Span` from
OpenTelemetry specification. This results from the fact that, Traces in
OpenTelemetry .NET is a somewhat unique implementation of the OpenTelemetry
project, as most of the [Trace
API](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md)
is implemented by the [.NET runtime](https://github.com/dotnet/runtime) itself.
From a high level, what this means is that you can instrument your application
by simply depending on `System.Diagnostics.DiagnosticSource` package, which
provides `Activity` and `ActivitySource` classes representing the OpenTelemetry
concepts of
[Span](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#span)
and
[Tracer](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracer)
respectively. Read
[this](../../../src/OpenTelemetry.Api/README.md#introduction-to-opentelemetry-net-tracing-api)
to learn more.

## Learn more

* If you want to customize the Sdk, refer to [customizing
  the SDK](../customizing-the-sdk/README.md).
* If you want to build a custom exporter/processor/sampler, refer to [extending
  the SDK](../extending-the-sdk/README.md).
