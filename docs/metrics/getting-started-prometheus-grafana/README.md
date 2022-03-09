# Getting Started with Prometheus and Grafana

- [Export metrics from the application](#export-metrics-from-the-application)
  - [Check results in the browser](#check-results-in-the-browser)
- [Collect metrics using Prometheus](#collect-metrics-using-prometheus)
  - [Configuration](#configuration)
  - [Start Prometheus](#start-prometheus)
  - [View results in Prometheus](#view-results-in-prometheus)
- [Explore metrics using Grafana](#explore-metrics-using-grafana)
- [Learn more](#learn-more)

## Export metrics from the application

It is highly recommended to go over the [getting-started](../getting-started/README.md)
doc before following along this document.

Create a new console application and run it:

```sh
dotnet new console --output getting-started-prometheus
cd getting-started-prometheus
dotnet run
```

Add a reference to [Prometheus
Exporter](../../../src/OpenTelemetry.Exporter.Prometheus/README.md):

```sh
dotnet add package OpenTelemetry.Exporter.Prometheus --version 1.2.0-rc2
```

Now, we are going to make some small tweaks to the example in the
getting-started metrics `Program.cs` to make the metrics available via
OpenTelemetry Prometheus Exporter.

First, copy and paste everything from getting-started
metrics [example](../getting-started/Program.cs) to the Program.cs file of the
new console application (getting-started-prometheus) we've created.

And replace the below line:

```csharp
.AddConsoleExporter()
```

with

```csharp
.AddPrometheusExporter(options => { options.StartHttpListener = true; })
```

With `AddPrometheusExporter()`, OpenTelemetry `PrometheusExporter` will export
data via the endpoint defined by
[PrometheusExporterOptions.HttpListenerPrefixes](../../../src/OpenTelemetry.Exporter.Prometheus/README.md#httplistenerprefixes),
which is `http://localhost:9464/` by default.

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

After the above modifications, now our `Program.cs` should look like [this](./Program.cs).

### Check results in the browser

Start the application and keep it running. Now we should be able to see the
metrics at [http://localhost:9464/metrics](http://localhost:9464/metrics) from a
web browser:

![Browser UI](https://user-images.githubusercontent.com/17327289/151633547-736c6d91-62d2-4e66-a53f-2e16c44bfabc.png)

Now, we understand how we can configure `PrometheusExporter` to export metrics.
Next, we are going to learn about how to use Prometheus to collect the metrics.

## Collect metrics using Prometheus

Follow the [first steps](https://prometheus.io/docs/introduction/first_steps/)
to download the [latest release](https://prometheus.io/download/) of Prometheus.

### Configuration

After finished downloading, extract it to a local location that's easy to
access. We will find the default Prometheus configuration YAML file in the
folder, named `prometheus.yml`.

Let's create a new file in the same location as where `prometheus.yml` locates,
and named the new file as `otel.yml` for this exercise. Then, copy and paste the
entire content below into the `otel.yml` file we have created just now.

```yaml
global:
  scrape_interval: 10s
  evaluation_interval: 10s
scrape_configs:
  - job_name: "otel"
    static_configs:
      - targets: ["localhost:9464"]
```

### Start Prometheus

Follow the instructions from
[starting-prometheus](https://prometheus.io/docs/introduction/first_steps/#starting-prometheus)
to start the Prometheus server and verify it has been started successfully.

Please note that we will need pass in `otel.yml` file as the argument:

```console
./prometheus --config.file=otel.yml
```

### View results in Prometheus

To use the graphical interface for viewing our metrics with Prometheus, navigate
to [http://localhost:9090/graph](http://localhost:9090/graph), and type
`MyFruitCounter` in the expression bar of the UI; finally, click the execute
button.

We should be able to see the following chart from the browser:

![Prometheus UI](https://user-images.githubusercontent.com/17327289/151636225-6e4ce4c7-09f3-4996-8ca5-d404a88d9195.png)

From the legend, we can see that the `instance` name and the `job` name are the
values we have set in `otel.yml`.

Congratulations!

Now we know how to configure Prometheus server and deploy OpenTelemetry
`PrometheusExporter` to export our metrics. Next, we are going to explore a tool
called Grafana, which has powerful visualizations for the metrics.

## Explore metrics using Grafana

[Install Grafana](https://grafana.com/docs/grafana/latest/installation/).

Start the standalone Grafana server (`grafana-server.exe` or
`./bin/grafana-server`, depending on the operating system). Then, use the
browser to navigate to [http://localhost:3000/](http://localhost:3000/).

Follow the instructions in the Grafana getting started
[doc](https://grafana.com/docs/grafana/latest/getting-started/getting-started/#step-2-log-in)
to log in.

After successfully logging in, click on the Configuration icon
on the panel at the left hand side, and click on Prometheus.
Type in the default endpoint of Prometheus as suggested by the UI
as the value for the URI.

```console
http://localhost:9090
```

Then, click on the Explore icon on the left panel of
the website - we should be able to write some queries to explore our metrics
now!

Feel free to find some handy PromQL
[here](https://promlabs.com/promql-cheat-sheet/).

In the below example, the query targets to find out what is the per-second rate
of increase of myFruitCounter over the past 5 minutes:

![Grafana
UI](https://user-images.githubusercontent.com/17327289/151636769-138ecb4f-b44f-477b-88eb-247fc4340252.png)

## Learn more

- [What is Prometheus?](https://prometheus.io/docs/introduction/overview/)
- [Grafana support for
  Prometheus](https://prometheus.io/docs/visualization/grafana/#creating-a-prometheus-graph)
