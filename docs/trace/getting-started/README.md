# Getting Started with OpenTelemetry .NET in 5 Minutes

First, download and install the [.NET Core
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
dotnet add package OpenTelemetry.Exporter.Console -v 1.0.0-rc1.1
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
```

Congratulations! You are now collecting traces using OpenTelemetry.

What does the above program do?

The program creates an `ActivitySource` which represents an [OpenTelemetry
Tracer](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracer).
The `ActivitySource` instance is used to start an `Activity` which represents an
[OpenTelemetry
Span](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#span).
An OpenTelemetry
[TracerProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracerprovider)
is configured to subscribe to the activities from the source
`MyCompany.MyProduct.MyLibrary`, and export it to `ConsoleExporter`.
`ConsoleExporter` simply displays it on the console.

## OpenTelemetry .NET and relation with .NET Activity API

If you tried the above program, you may have already noticed that the terms
`ActivitySource` and `Activity` were used instead of `Tracer` and `Span` from
OpenTelemetry specification. This results from the fact that, OpenTelemetry .NET
is a somewhat unique implementation of the OpenTelemetry project, as parts of
the tracing API are incorporated directly into the .NET runtime itself. From a
high level, what this means is that the `Activity` and `ActivitySource` classes
from .NET runtime represent the OpenTelemetry concepts of
[Span](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#span)
and
[Tracer](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracer)
respectively. Read
[this](../../../src/OpenTelemetry.Api/README.md#introduction-to-opentelemetry-net-tracing-api)
to learn more.
