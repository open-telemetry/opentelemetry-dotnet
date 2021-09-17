# Getting Started with Metrics

Then show how to add each of the 4 instruments available

- Counter
- ObservableCounter
- Histogram
- Gauge

Extract common code related to adding metrics to a program.

## Setup

1. `Counter`
1. Update `Program.cs` with the following

    ```{literalinclude} ../../metrics/getting-started-counter/Program.cs
    :language: c#
    :lines: 17-
    ```

1. Run the application

    ```sh
    dotnet run
    ```

1. You should see the following output
    <!-- markdownlint-disable -->
    ```text
    Export MyCounter, Meter: MyCompany.MyProduct.MyLibrary/1.0
    2021-09-17T18:12:32.5817665Z, 2021-09-17T18:12:42.7306718Z] tag1:value1tag2:value2 LongSum
    Value: 20000000
    Instrument MyCompany.MyProduct.MyLibrary:MyCounter completed.
    ```
    <!-- markdownlint-enable -->

Congratulations! You are now collecting metrics using OpenTelemetry.

## What does the above program do?

The program creates a
[Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
instance named `TestMeter` and then creates a
[Counter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#counter)
instrument from it. This counter is used to repeatedly report metric measurements
until exited after `10 seconds`.

An OpenTelemetry
[MeterProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meterprovider)
is configured to subscribe to instruments from the Meter `TestMeter`, and
aggregate the measurements in-memory. The pre-aggregated metrics are exported
every `1 second` to a `ConsoleExporter`.
