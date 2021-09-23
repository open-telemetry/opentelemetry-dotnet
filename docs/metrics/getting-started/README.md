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
dotnet add package --prerelease OpenTelemetry.Exporter.Console
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs):

Run the application again (using `dotnet run`) and you should see the metric
output from the console, similar to shown below:

<!-- markdownlint-disable MD013 -->
```text
Export MyCounter, Meter: MyCompany.MyProduct.MyLibrary/1.0
2021-09-23T02:20:58.8168802Z, 2021-09-23T02:20:58.8359677Z] color:orangename:orange LongSum
Value: 6
2021-09-23T02:20:58.8168802Z, 2021-09-23T02:20:58.8359677Z] color:yellowname:lemon LongSum
Value: 2
2021-09-23T02:20:58.8168802Z, 2021-09-23T02:20:58.8359677Z] color:greenname:lime LongSum
Value: 11
2021-09-23T02:20:58.8168802Z, 2021-09-23T02:20:58.8359677Z] color:greenname:orange LongSum
Value: 2
```
<!-- markdownlint-enable MD013 -->

Congratulations! You are now collecting metrics using OpenTelemetry.

What does the above program do?

The program creates a
[Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
instance named "MyCompany.MyProduct.MyLibrary" and then creates a
[Counter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#counter)
instrument from it. This counter is used to report several metric measurements.

An OpenTelemetry
[MeterProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meterprovider)
is configured to subscribe to instruments from the Meter
`MyCompany.MyProduct.MyLibrary`, and aggregate the measurements in-memory. The
pre-aggregated metrics are exported to a `ConsoleExporter`.
