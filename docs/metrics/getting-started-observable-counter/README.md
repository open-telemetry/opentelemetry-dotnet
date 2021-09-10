# Getting Started with OpenTelemetry .NET in 5 Minutes

First, download and install the [.NET Core
SDK](https://dotnet.microsoft.com/download) on your computer.

Create a new console application and run it:

```sh
dotnet new console --output getting-started-observable-counter
cd getting-started-observable-counter
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
Service.Nameunknown_service:getting-started-observable-counter
Export 16:35:25.669 16:35:25.670 observable-counter [tag1=value1;tag2=value2] LongSum, Meter: TestMeter/0.0.1
Value: 10
Export 16:35:25.669 16:35:26.698 observable-counter [tag1=value1;tag2=value2] LongSum, Meter: TestMeter/0.0.1
Value: 20
Export 16:35:25.669 16:35:27.711 observable-counter [tag1=value1;tag2=value2] LongSum, Meter: TestMeter/0.0.1
Value: 30
Export 16:35:25.669 16:35:28.729 observable-counter [tag1=value1;tag2=value2] LongSum, Meter: TestMeter/0.0.1
Value: 40
```
<!-- markdownlint-enable MD013 -->

Congratulations! You are now collecting metrics using OpenTelemetry.

What does the above program do?

The program creates a
[Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
instance named "TestMeter" and then creates a
[Asynchronous Counter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#asynchronous-counter)
instrument from it. This Counter reports an ever increasing number as its
measurement until exited after 10 seconds.

An OpenTelemetry
[MeterProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meterprovider)
is configured to subscribe to instruments from the Meter `TestMeter`, and
aggregate the measurements in-memory. The pre-aggregated metrics are exported
every 1 second to a `ConsoleExporter`. `ConsoleExporter` simply displays it on
the console.
