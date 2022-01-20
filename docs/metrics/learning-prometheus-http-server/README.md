# Quick start on exporting metrics to Prometheus/Grafana

- [Quick start on exporting metrics to Prometheus/Grafana](#quick-start-on-exporting-metrics-to-prometheusgrafana)
  - [Prerequisite](#prerequisite)
  - [Introduction](#introduction)
  - [Get Prometheus](#get-prometheus)
  - [Prometheus HTTP server](#prometheus-http-server)
    - [Configuration](#configuration)
    - [Start Prometheus](#start-prometheus)
    - [Configure OpenTelemetry to Expose metrics to Prometheus Endpoint](#configure-opentelemetry-to-expose-metrics-to-prometheus-endpoint)
    - [Check Results in Prometheus](#check-results-in-prometheus)
    - [View/Query Results with Grafana](#viewquery-results-with-grafana)

## Prerequisite

It is highly recommended to go over the [getting-started](../getting-started/)
project under the metrics document folder before following along this document.

## Introduction

- [What is Prometheus?](https://prometheus.io/docs/introduction/overview/)

- [Grafana support for
  Prometheus](https://prometheus.io/docs/visualization/grafana/#creating-a-prometheus-graph)

## Get Prometheus

Follow the [first steps]((https://prometheus.io/docs/introduction/first_steps/))
in the prometheus official document.

## Prometheus HTTP server

### Configuration

After downloading the [latest release](https://prometheus.io/download/), extract
it to a local location that's easy to access. We will find the default
prometheus configuration yaml file in the folder, named `prometheus.yml`.

Replace all the content with:
```
global:
  scrape_interval: 10s
  scrape_timeout: 10s
  evaluation_interval: 10s
scrape_configs:
- job_name: OpenTelemetryTest # assign a job name, as prometheus server can listen to multiple jobs
  honor_timestamps: true
  scrape_interval: 1s
  scrape_timeout: 1s
  metrics_path: /metrics
  scheme: http
  follow_redirects: true
  static_configs:
  - targets:
    - localhost:9184 # set the port that prometheus server will listen to
```

### Start Prometheus

Follow the instructions from
[starting-prometheus](https://prometheus.io/docs/introduction/first_steps/#starting-prometheus)
to start the prometheus server and verify it has been started successfully.

Once the server is started, we are going to make some small tweaks to the
example in the getting-started metrics [example](../getting-started/Program.cs)
to export our metrics to the endpoint that prometheus was configured to listen
to.

### Configure OpenTelemetry to Expose metrics to Prometheus Endpoint 

Create a new console application and run it:

```sh
dotnet new console --output prometheus-http-server
cd prometheus-http-server
dotnet run
```

We will have to add a reference to prometheus exporter to the
prometheus-http-server application.

```shell
dotnet add package OpenTelemetry.Exporter.Prometheus --version 1.2.0-rc1
```

Now, replace the below line in the [Program.cs](../getting-started/Program.cs)

```csharp
.AddConsoleExporter()
```

with

```csharp
.AddPrometheusExporter(opt =>
{
    opt.StartHttpListener = true;
    opt.HttpListenerPrefixes = new string[] { $"http://localhost:9184/" };
})
```

We set the options for our prometheus exporter to export data via the endpoint
that we've configured prometheus server to listen to in the prometheus.yml file.

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

After the above modifications, now our `program.cs` should look like [this](./Program.cs).

### Check Results in Prometheus

Execute the application and leave the process running in the background. Now we
should be able to see the metrics at the endpoint we've configured in
`prometheus.yml` file and defined in `Program.cs`; in this case, the endpoint
is: "http://localhost:9184/". 

Check the output metrics with your favorite browser:

![MyFruitCounter](./img/myFruitCounter.PNG)

To use the graphical interface for viewing your metrics with Prometheus,
navigate to "http://localhost:9090/graph", type `MyFruitCounter` in the
expression bar of the UI, and click execute. 

We should be able to see the following chart:

![Prometheus Graph on myFruitCounter](./img/prometheusGraph.PNG)

From the legend, we can see that the `instance` name and the `job` name are the
values we have set in `prometheus.yml` file.

Congratulations!

Now we know how to configure prometheus http server and deploy OpenTelemetry
prometheusExporter to export our metrics. Next, we are going to explore a tool
called Grafana, which has powerful visualizations for the metrics.

### View/Query Results with Grafana

First of all, please [Install Grafana](https://grafana.com/docs/grafana/latest/installation/).

For windows users, after finishing installation, start the standalone Grafana
server, grafana-server.exe located in the bin folder. Then, use the browser to
navigate to the default port of grafana `3000`. We can confirm the port number
with the logs from the command line after starting the grafana server as well. 

And follow the instructions in the grafana getting started
[doc](https://grafana.com/docs/grafana/latest/getting-started/getting-started/#step-2-log-in)
to log in.

After successfully logging in, click on the explore option on the left panel of
the website - we should be able to write some queries to explore our metrics
now!

Feel free to find some handy PromQL
[here](https://promlabs.com/promql-cheat-sheet/).

In the below example, the query targeted to find out what is the per-second rate
of increace for myFruitCounter over the last 30 minutes:

![Grafana dashboard with myFruitCounter metrics rate](./img/grafana.PNG)
