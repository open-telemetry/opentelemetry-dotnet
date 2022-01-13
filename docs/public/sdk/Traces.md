# Traces

## Setup

1. Update `Program.cs` with the following

    ```{literalinclude} ../../trace/getting-started/Program.cs
    :language: c#
    :lines: 29-
    ```

1. Run the application

    ```sh
    dotnet run
    ```

1. You should see the following output

    ```{literalinclude} ../../trace/getting-started/Program.cs
    :language: text
    :lines: 18-26
    ```

Congratulations! You are now collecting traces using OpenTelemetry.

## What does the above program do

The program creates an `ActivitySource` which represents an
[OpenTelemetry Tracer](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracer).
The `ActivitySource` instance is used to start an `Activity` which represents an
[OpenTelemetry Span](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#span).
An OpenTelemetry
[TracerProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracerprovider)
is configured to subscribe to the activities from the source
`MyCompany.MyProduct.MyLibrary`, and export it to `ConsoleExporter`.

## OpenTelemetry .NET and Relation with .NET Activity API

<!-- TODO: similar comment is made at bottom of Prerequisites page -->

If you tried the above program, you may have already noticed that the terms
`ActivitySource` and `Activity` were used instead of `Tracer` and `Span` from
OpenTelemetry specification. This results from the fact that,  OpenTelemetry
.NET is a somewhat unique implementation of the OpenTelemetry project, as parts
of the tracing API are incorporated directly into the .NET runtime itself. From
a high level, what this means is that the `Activity` and `ActivitySource`
classes from .NET runtime represent the OpenTelemetry concepts of
[Span](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#span)
and
[Tracer](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/api.md#tracer)
respectively. Read
[src/OpenTelemetry.Api/README.md](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Api/README.md#introduction-to-opentelemetry-net-tracing-api)
to learn more.
