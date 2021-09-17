# Getting Started with Metrics

Then show how to add each of the 4 instruments available

1. Counter
1. ObservableCounter
1. Histogram
1. Gauge

Extract common code related to adding metrics to a program.

## Setup

1. `Counter`
1. Update `Program.cs` with the following

    ```c#
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Threading;
    using System.Threading.Tasks;
    using OpenTelemetry;
    using OpenTelemetry.Metrics;

    public class Program
    {
        private static readonly Meter MyMeter = new Meter("TestMeter", "0.0.1");
        private static readonly Counter<long> Counter = MyMeter.CreateCounter<long>("counter");

        public static async Task Main(string[] args)
        {
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                    .AddSource("TestMeter")
                    .AddConsoleExporter()
                    .Build();

            using var token = new CancellationTokenSource();
            Task writeMetricTask = new Task(() =>
            {
                while (!token.IsCancellationRequested)
                {
                    Counter.Add(
                                10,
                                new KeyValuePair<string, object>("tag1", "value1"),
                                new KeyValuePair<string, object>("tag2", "value2"));
                    Task.Delay(10).Wait();
                }
            });
            writeMetricTask.Start();

            token.CancelAfter(10000);
            await writeMetricTask;
        }
    }
    ```

1. Run the application

    ```sh
    dotnet run
    ```

1. You should see the following output

    ```text
    Export counter, Meter: TestMeter/0.0.1
    (2021-09-03T04:29:42.1791523Z, 2021-09-03T04:29:43.1875033Z]
      tag1:value1tag2:value2 LongSum
    Value: 620
    ```

Congratulations! You are now collecting metrics using OpenTelemetry.

<!-- TODO Can we just import the Program.cs file (skip copyright would be nice too) -->

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
every `1 second` to a `ConsoleExporter`. `ConsoleExporter` simply displays it
on the console.
