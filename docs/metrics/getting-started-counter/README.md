# Getting Started with OpenTelemetry .NET in 5 Minutes

First, download and install the [.NET Core
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new console application and run it:

```sh
dotnet new console --output getting-started-counter
cd getting-started-counter
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
dotnet add package --prerelease OpenTelemetry.Exporter.Console
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs):

Run the application again (using `dotnet run`) and you should see the metric
output from the console, similar to shown below:

<!-- markdownlint-disable MD013 -->
```text
Export counter, Meter: TestMeter/0.0.1
(2021-09-03T04:29:37.1096398Z, 2021-09-03T04:29:38.0649233Z] tag1:value1tag2:value2 LongSum
Value: 460

Export counter, Meter: TestMeter/0.0.1
(2021-09-03T04:29:38.0649233Z, 2021-09-03T04:29:39.1483639Z] tag1:value1tag2:value2 LongSum
Value: 640

Export counter, Meter: TestMeter/0.0.1
(2021-09-03T04:29:39.1483639Z, 2021-09-03T04:29:40.1679696Z] tag1:value1tag2:value2 LongSum
Value: 420

Export counter, Meter: TestMeter/0.0.1
(2021-09-03T04:29:40.1679696Z, 2021-09-03T04:29:41.1774527Z] tag1:value1tag2:value2 LongSum
Value: 560

Export counter, Meter: TestMeter/0.0.1
(2021-09-03T04:29:41.1774527Z, 2021-09-03T04:29:42.1791523Z] tag1:value1tag2:value2 LongSum
Value: 650

Export counter, Meter: TestMeter/0.0.1
(2021-09-03T04:29:42.1791523Z, 2021-09-03T04:29:43.1875033Z] tag1:value1tag2:value2 LongSum
Value: 620
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
