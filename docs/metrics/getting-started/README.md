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
dotnet add package OpenTelemetry.Exporter.Console
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs):

Run the application again (using `dotnet run`) and you should see the metric
output from the console, similar to shown below:

<!-- markdownlint-disable MD013 -->
```text
Export[] 16:38:36.241 16:38:37.233 TestMeter:counter [tag1=value1;tag2=value2] SumMetricAggregator Value: 590, Details: Delta=True,Mon=True,Count=59,Sum=590
Export[] 16:38:37.233 16:38:38.258 TestMeter:counter [tag1=value1;tag2=value2] SumMetricAggregator Value: 640, Details: Delta=True,Mon=True,Count=64,Sum=640
Export[] 16:38:38.258 16:38:39.261 TestMeter:counter [tag1=value1;tag2=value2] SumMetricAggregator Value: 640, Details: Delta=True,Mon=True,Count=64,Sum=640
Export[] 16:38:39.261 16:38:40.266 TestMeter:counter [tag1=value1;tag2=value2] SumMetricAggregator Value: 630, Details: Delta=True,Mon=True,Count=63,Sum=630
Export[] 16:38:40.266 16:38:41.271 TestMeter:counter [tag1=value1;tag2=value2] SumMetricAggregator Value: 640, Details: Delta=True,Mon=True,Count=64,Sum=640
```
<!-- markdownlint-enable MD013 -->

Congratulations! You are now collecting metrics using OpenTelemetry.

What does the above program do?

The program creates a
[Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
instance named "TestMeter" and then creates a
[Counter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#counter)
instrument from it. This counter is used to repeatedly report metric
measurements until exited after 10 seconds.

An OpenTelemetry
[MeterProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meterprovider)
is configured to subscribe to instruments from the Meter `TestMeter`, and
aggregate the measurements in-memory. The pre-aggregated metrics are exported
every 1 second to a `ConsoleExporter`. `ConsoleExporter` simply displays it on
the console.
