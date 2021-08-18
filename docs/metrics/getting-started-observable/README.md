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
Export 18:21:47.330 18:21:47.332 observable-counter [tag1=value1;tag2=value2] Summary Value: Sum: 10 Count: 1, Meter: TestMeter/0.0.1
Export 18:21:47.332 18:21:48.353 observable-counter [tag1=value1;tag2=value2] Summary Value: Sum: 10 Count: 1, Meter: TestMeter/0.0.1
Export 18:21:48.353 18:21:49.353 observable-counter [tag1=value1;tag2=value2] Summary Value: Sum: 10 Count: 1, Meter: TestMeter/0.0.1
Export 18:21:49.353 18:21:50.366 observable-counter [tag1=value1;tag2=value2] Summary Value: Sum: 10 Count: 1, Meter: TestMeter/0.0.1
Export 18:21:50.366 18:21:51.373 observable-counter [tag1=value1;tag2=value2] Summary Value: Sum: 10 Count: 1, Meter: TestMeter/0.0.1
Export 18:21:51.373 18:21:52.387 observable-counter [tag1=value1;tag2=value2] Summary Value: Sum: 10 Count: 1, Meter: TestMeter/0.0.1
Export 18:21:52.387 18:21:53.401 observable-counter [tag1=value1;tag2=value2] Summary Value: Sum: 10 Count: 1, Meter: TestMeter/0.0.1
Export 18:21:53.401 18:21:54.407 observable-counter [tag1=value1;tag2=value2] Summary Value: Sum: 10 Count: 1, Meter: TestMeter/0.0.1
Export 18:21:54.407 18:21:55.423 observable-counter [tag1=value1;tag2=value2] Summary Value: Sum: 10 Count: 1, Meter: TestMeter/0.0.1
Export 18:21:55.423 18:21:56.435 observable-counter [tag1=value1;tag2=value2] Summary Value: Sum: 10 Count: 1, Meter: TestMeter/0.0.1
```
<!-- markdownlint-enable MD013 -->

Congratulations! You are now collecting metrics using OpenTelemetry.

What does the above program do?

The program creates a
[Meter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meter)
instance named "TestMeter" and then creates an
[Observable Counter](https://github.com/open-telemetry/opentelemetry-specification/blob/05cdd67c8b0d30ecf9c04254ce15c29b98cf4c9c/specification/metrics/api.md#asynchronous-counter)
instrument from it.
This observable counter will be called on to report metrics repeatedly
until program exits after 10 seconds.

An OpenTelemetry
[MeterProvider](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/api.md#meterprovider)
is configured to subscribe to instruments from the Meter `TestMeter`, and
aggregate the measurements in-memory. The pre-aggregated metrics are exported
every 1 second to a `ConsoleExporter`. `ConsoleExporter` simply displays it on
the console.
