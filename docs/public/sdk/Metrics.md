# Metrics

## Setup

1. Update `Program.cs` with the following

    ```{literalinclude} ../../metrics/getting-started/Program.cs
    :language: c#
    :lines: 24-
    ```

1. Run the application

    ```sh
    dotnet run
    ```

1. You should see the following output

    ```{literalinclude} ../../metrics/getting-started/Program.cs
    :language: text
    :lines: 18-21
    ```

Congratulations! You are now collecting metrics using OpenTelemetry.

## What does the above program do

The program creates a
[Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
instance named `MyCompany.MyProduct.MyLibrary` and then creates a
[Counter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#counter)
instrument from it. This counter is used to report several metric measurements.

An OpenTelemetry
[MeterProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meterprovider)
is configured to subscribe to instruments from the Meter
`MyCompany.MyProduct.MyLibrary`, and aggregate the measurements in-memory.
The pre-aggregated metrics are exported to a `ConsoleExporter`.
