# Getting Started with Prometheus and Grafana

- [Export metrics from the application](#export-metrics-from-the-application)
  - [Check results in the console](#check-results-in-the-console)
- [Collect metrics using Prometheus](#collect-metrics-using-prometheus)
  - [Install and run Prometheus](#install-and-run-prometheus)
  - [View results in Prometheus](#view-results-in-prometheus)
- [Explore metrics using Grafana](#explore-metrics-using-grafana)
- [Final cleanup](#final-cleanup)
- [Learn more](#learn-more)

## Export metrics from the application

It is highly recommended to go over the [getting started in 5 minutes - Console
Application](../getting-started-console/README.md) doc before following along
this document.

Create a new console application and run it:

```sh
dotnet new console --output getting-started-prometheus
cd getting-started-prometheus
dotnet run
```

Add reference to [Console
Exporter](../../../src/OpenTelemetry.Exporter.Console/README.md) and [OTLP
Exporter](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md):

```sh
dotnet add package OpenTelemetry.Exporter.Console
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Now copy the code from [Program.cs](./Program.cs).

### Check results in the console

Run the application again and we should see the metrics output from the console:

```text
> dotnet run
Press any key to exit

Resource associated with Metric:
    telemetry.sdk.name: opentelemetry
    telemetry.sdk.language: dotnet
    telemetry.sdk.version: 1.6.1-alpha.0.23
    service.name: unknown_service:getting-started-prometheus-grafana

Export MyFruitCounter, Meter: MyCompany.MyProduct.MyLibrary/1.0
(2023-09-22T20:40:22.2586791Z, 2023-09-22T20:40:31.1582923Z] color: red name: apple LongSum
Value: 54
(2023-09-22T20:40:22.2586791Z, 2023-09-22T20:40:31.1582923Z] color: yellow name: lemon LongSum
Value: 63
(2023-09-22T20:40:22.2586791Z, 2023-09-22T20:40:31.1582923Z] color: green name: apple LongSum
Value: 18

...
```

Note that we have configured two exporters in the code:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    ...
    .AddConsoleExporter()
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("http://localhost:9090/api/v1/otlp/v1/metrics");
        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
    })
    .Build();
```

When we run the application, the `ConsoleExporter` was printing the metrics on
console, and the `OtlpExporter` was attempting to send the metrics to
`http://localhost:9090/api/v1/otlp/v1/metrics`.

Since we didn't have Prometheus server running, the metrics received by
`OtlpExporter` were simply dropped on the floor. In the next step, we are going
to learn about how to use Prometheus to collect and visualize the metrics.

```mermaid
graph LR

subgraph SDK
  MeterProvider
  MetricReader[BaseExportingMetricReader]
  MetricReader2[BaseExportingMetricReader]
  ConsoleExporter
  OtlpExporter
end

subgraph API
  Instrument["Meter(#quot;MyCompany.MyProduct.MyLibrary#quot;, #quot;1.0#quot;)<br/>Counter(#quot;MyFruitCounter#quot;)"]
end

Instrument --> | Measurements | MeterProvider

MeterProvider --> | Metrics | MetricReader --> | Push | OtlpExporter

MeterProvider --> | Metrics | MetricReader2 --> | Push | ConsoleExporter
```

Also, for our learning purpose, use a while-loop to keep increasing the counter
value until any key is pressed.

```csharp
Console.WriteLine("Press any key to exit");
while (!Console.KeyAvailable)
{
    Thread.Sleep(1000);
    MyFruitCounter.Add(1, new("name", "apple"), new("color", "red"));
    MyFruitCounter.Add(2, new("name", "lemon"), new("color", "yellow"));
    MyFruitCounter.Add(1, new("name", "lemon"), new("color", "yellow"));
    ...
    ...
    ...
}
```

## Collect metrics using Prometheus

### Install and run Prometheus

Follow the [first steps](https://prometheus.io/docs/introduction/first_steps/)
to download the [latest release](https://prometheus.io/download/) of Prometheus.

After finished downloading, extract it to a local location that's easy to
access. Run the `prometheus(.exe)` server executable with feature flag
[otlp-receiver](https://prometheus.io/docs/prometheus/latest/feature_flags/#otlp-receiver)
enabled:

```sh
./prometheus --enable-feature=otlp-write-receiver
```

### View results in Prometheus

To use the graphical interface for viewing our metrics with Prometheus, navigate
to [http://localhost:9090/graph](http://localhost:9090/graph), and type
`MyFruitCounter_total` in the expression bar of the UI; finally, click the
execute button.

We should be able to see the following chart from the browser:

![Prometheus UI](https://user-images.githubusercontent.com/17327289/151636225-6e4ce4c7-09f3-4996-8ca5-d404a88d9195.png)

Congratulations!

Now we know how to configure Prometheus server and deploy OpenTelemetry
`OtlpExporter` to export our metrics. Next, we are going to explore a tool
called Grafana, which has powerful visualizations for the metrics.

## Explore metrics using Grafana

[Install Grafana](https://grafana.com/docs/grafana/latest/installation/).

Start the standalone Grafana server (`grafana-server.exe` or
`./bin/grafana-server`, depending on the operating system). Then, use the
browser to navigate to [http://localhost:3000/](http://localhost:3000/).

Follow the instructions in the Grafana getting started
[doc](https://grafana.com/docs/grafana/latest/getting-started/getting-started/#step-2-log-in)
to log in.

After successfully logging in, hover on the Configuration icon
on the panel at the left hand side, and click on Plugins.
Find and click on the Prometheus plugin. Next click on
`Create a Prometheus data source` button. Type in the default endpoint of
Prometheus as suggested by the UI as the value for the URI.

```console
http://localhost:9090
```

At the bottom of the page click `Save & test` to ensure the data source is
working. Then, click on the `Explore` button - we should be able to write
some queries to explore our metrics now!

Feel free to find some handy PromQL
[here](https://promlabs.com/promql-cheat-sheet/).

In the below example, the query targets to find out what is the per-second rate
of increase of `MyFruitCounter_total` over the past 5 minutes:

![Grafana
UI](https://user-images.githubusercontent.com/17327289/151636769-138ecb4f-b44f-477b-88eb-247fc4340252.png)

## Final cleanup

In the end, remove the Console Exporter so we only have OTLP Exporter in the
final application:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    ...
    // Remove Console Exporter from the final application
    // .AddConsoleExporter()
    .AddOtlpExporter(options =>
    {
        options.Endpoint = new Uri("http://localhost:9090/api/v1/otlp/v1/metrics");
        options.Protocol = OpenTelemetry.Exporter.OtlpExportProtocol.HttpProtobuf;
    })
    .Build();
```

```sh
dotnet remove package OpenTelemetry.Exporter.Console
```

```mermaid

graph LR
subgraph SDK
  MeterProvider
  MetricReader[BaseExportingMetricReader]
  OtlpExporter
end

subgraph API
  Instrument["Meter(#quot;MyCompany.MyProduct.MyLibrary#quot;, #quot;1.0#quot;)<br/>Counter(#quot;MyFruitCounter#quot;)"]
end

Instrument --> | Measurements | MeterProvider

MeterProvider --> | Metrics | MetricReader --> | Push | OtlpExporter
```

## Learn more

- [What is Prometheus?](https://prometheus.io/docs/introduction/overview/)
- [Grafana support for
  Prometheus](https://prometheus.io/docs/visualization/grafana/#creating-a-prometheus-graph)
