# Getting Started with OpenTelemetry .NET in 5 Minutes

First, download and install the [.NET Core
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new console application and run it:

```sh
dotnet new console --output getting-started-histogram
cd getting-started-histogram
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
dotnet add package OpenTelemetry.Exporter.Console --version 1.2.*.*
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs):

Run the application again (using `dotnet run`) and you should see the metric
output from the console, similar to shown below:

<!-- markdownlint-disable MD013 -->
```text
Export 14:30:58.201 14:30:59.177 histogram [tag1=value1;tag2=value2] Histogram, Meter: TestMeter/0.0.1
Value: Sum: 33862 Count: 62
(-? - 0) : 0
(0 - 5) : 0
(5 - 10) : 0
(10 - 25) : 2
(25 - 50) : 0
(50 - 75) : 1
(75 - 100) : 1
(100 - 250) : 6
(250 - 500) : 18
(500 - 1000) : 34
(1000 - ?) : 0
```
<!-- markdownlint-enable MD013 -->

Congratulations! You are now collecting histogram metrics using OpenTelemetry.

What does the above program do?

The program creates a
[Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
instance named "TestMeter" and then creates a
[Histogram](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#histogram)
instrument from it. This histogram is used to repeatedly report random metric
measurements until exited after 10 seconds.

An OpenTelemetry
[MeterProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meterprovider)
is configured to subscribe to instruments from the Meter `TestMeter`, and
aggregate the measurements in-memory. The pre-aggregated metrics are exported
every 1 second to a `ConsoleExporter`. `ConsoleExporter` simply displays it on
the console.
