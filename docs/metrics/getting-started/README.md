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
[OpenTelemetry](../../../src/OpenTelemetry/README.md)
package:

```sh
dotnet add package OpenTelemetry
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs):

Run the application again (using `dotnet run`) and you should see the metric
output from the console, similar to shown below:

<!-- markdownlint-disable MD013 -->
```text
Export[] 14:54:56.162 14:54:57.155 TestMeter:counter [tag1=value1;tag2=value2] SumMetricAggregator Value: 15977610, Details: Delta=True,Mon=True,Count=1597761,Sum=15977610
Export[] 14:54:57.155 14:54:58.184 TestMeter:counter [tag1=value1;tag2=value2] SumMetricAggregator Value: 21656160, Details: Delta=True,Mon=True,Count=2165616,Sum=21656160
Export[] 14:54:58.184 14:54:59.195 TestMeter:counter [tag1=value1;tag2=value2] SumMetricAggregator Value: 20273630, Details: Delta=True,Mon=True,Count=2027363,Sum=20273630
Export[] 14:54:59.195 14:55:00.209 TestMeter:counter [tag1=value1;tag2=value2] SumMetricAggregator Value: 19113300, Details: Delta=True,Mon=True,Count=1911330,Sum=19113300
Export[] 14:55:00.209 14:55:01.220 TestMeter:counter [tag1=value1;tag2=value2] SumMetricAggregator Value: 17327600, Details: Delta=True,Mon=True,Count=1732760,Sum=17327600
```
<!-- markdownlint-enable MD013 -->

Congratulations! You are now collecting metrics using OpenTelemetry.

What does the above program do?

The program creates a
[Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
instance named "TestMeter" and then creates a
[Counter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#counter)
instrument from it. This counter is used to repeatedly report metric measurements until exited.

An OpenTelemetry
[MeterProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meterprovider)
is configured to subscribe to instruments from the Meter `TestMeter`, and
aggregate the measurements in-memory. The pre-aggregated metrics are exported
every 1 second to a `ConsoleExporter`. `ConsoleExporter` simply displays it on
the console.
