# Getting Started with OpenTelemetry .NET in 5 Minutes

First, download and install the [.NET Core
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new console application and run it:

```sh
dotnet new console --output getting-started-async-gauge
cd getting-started-async-gauge
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
Service.Nameunknown_service:getting-started-gauge
Export 15:44:05.262 15:44:05.263 Gauge [tag1=value1;tag2=value2] LongGauge, Meter: TestMeter/0.0.1
Value: 306
Export 15:44:05.262 15:44:06.290 Gauge [tag1=value1;tag2=value2] LongGauge, Meter: TestMeter/0.0.1
Value: 693
Export 15:44:05.262 15:44:07.302 Gauge [tag1=value1;tag2=value2] LongGauge, Meter: TestMeter/0.0.1
Value: 78
```
<!-- markdownlint-enable MD013 -->

Congratulations! You are now collecting metrics using OpenTelemetry.

What does the above program do?

The program creates a
[Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
instance named "TestMeter" and then creates a
[Asynchronous Gauge](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#asynchronous-gauge)
instrument from it. This Gauge reports a randomnly generated number as its
measurement until exited after 10 seconds.

An OpenTelemetry
[MeterProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meterprovider)
is configured to subscribe to instruments from the Meter `TestMeter`, and
aggregate the measurements in-memory. The pre-aggregated metrics are exported
every 1 second to a `ConsoleExporter`. `ConsoleExporter` simply displays it on
the console.
