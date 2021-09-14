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
dotnet add package --prerelease OpenTelemetry.Exporter.Console
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs):

Run the application again (using `dotnet run`) and you should see the metric
output from the console, similar to shown below:

<!-- markdownlint-disable MD013 -->
```text
Export (2021-09-02T01:02:09.8013446Z, 2021-09-02T01:02:19.9749573Z] histogram tag1=value1;tag2=value2 Histogram, Meter: MyCompany.MyProduct.MyLibrary/1.0
Value: Sum: 304929 Count: 623
(-Infinity, 0]: 0
(0, 5]: 5
(5, 10]: 5
(10, 25]: 14
(25, 50]: 15
(50, 75]: 18
(75, 100]: 17
(100, 250]: 91
(250, 500]: 158
(500, 1000]: 300
(1000, +Infinity): 0
```
<!-- markdownlint-enable MD013 -->

Congratulations! You are now collecting histogram metrics using OpenTelemetry.

What does the above program do?

The program creates a
[Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
instance named "MyCompany.MyProduct.MyLibrary" and then creates a
[Histogram](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#histogram)
instrument from it. This histogram is used to repeatedly report random metric
measurements until it reaches a certain number of loops.

An OpenTelemetry
[MeterProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meterprovider)
is configured to subscribe to instruments from the Meter
`MyCompany.MyProduct.MyLibrary`, and aggregate the measurements in-memory. The
pre-aggregated metrics are exported every 1 second to a `ConsoleExporter`.
`ConsoleExporter` simply displays it on the console.
