# Using Exemplars in OpenTelemetry .NET

Exemplars are example data points for aggregated data. They provide specific
context to otherwise general aggregations. One common use case is to gain
ability to correlate metrics to traces (and logs). While OpenTelemetry .NET
supports Exemplars, it is only useful if the telemetry backend also supports the
capabilities. This tutorial uses well known open source backends to demonstrate
the concept. The following are the components involved:

* Test App - We use existing example app from the repo. This app is already
instrumented with OpenTelemetry for logs, metrics and traces, and is configured
to export them to the configured OTLP end point.
* OpenTelemetry Collector - An instance of collector is run, which receives
telemetry from the above app using OTLP. The collector then exports metrics to
Prometheus, traces to Tempo.
* Prometheus - Prometheus is used as the Metric backend.
* Tempo - Tempo is used as the Tracing backend.
* Grafana - UI to query metrics from Prometheus, traces from Tempo, and to
  navigate between metrics and traces using Exemplar.

All these components except the test app require additional configuration to
enable Exemplar feature. To make it easy for users, these components are
pre-configured to enable Exemplars, and a docker compose file is provided to
 spun them all up, in the required configurations.

## Pre-requisite

Install docker: <https://docs.docker.com/get-docker/>

## Setup

As mentioned in the intro, this tutorial uses OTel Collector, Prometheus, Tempo,
and Grafana, and they must be up and running before proceeding. The following
spins all of them with the correct configurations to support Exemplars.

Navigate to current directory and run the following:

```sh
docker compose up -d
```

If the above step succeeds, all dependencies would be spun up and ready now. To
test, navigate to Grafana running at: `http://localhost:3000/`.

## Run test app

Now that the required dependencies are ready, lets run the demo app.
This tutorial is using the existing ASP.NET Core app from the repo.

Navigate to [Example Asp.Net Core App](../../../examples/AspNetCore/Program.cs)
directory and run the following command:

```sh
dotnet run
```

Once the application is running, navigate to
[http://localhost:5000/weatherforecast]("http://localhost:5000/weatherforecast")
from a web browser. You may use the following Powershell script to generate load
to the application.

```powershell
while($true)
{
    Invoke-WebRequest http://localhost:5000/weatherforecast
    Start-Sleep -Milliseconds 500
}
```

## Use Exemplars to navigate from Metrics to Traces

The application sends metrics (with exemplars), and traces to the OTel
Collector, which export metrics and traces to Prometheus and Tempo
respectively.

Please wait for 2 minutes before continuing so that enough data is generated
and exported.

Open Grafana, select Explore, and select Prometheus as the source. Select the
metric named "http_server_duration_bucket", and plot the chart. Toggle on the
"Exemplar" option from the UI and hit refresh.

![Enable Exemplar](https://user-images.githubusercontent.com/16979322/218627781-9886f837-11ae-4d52-94d3-f1821503209c.png)

The Exemplars appear as special "diamond shaped dots" along with the metric
charts in the UI. Select any Exemplar to see the exemplar data, which includes
the timestamp when the measurement was recorded, the raw value, and trace
context when the recording was done. The "trace_id" enables jumping to the
tracing backed (tempo). Click on the "Query with Tempo" button next to the
"trace_id" field to open the corresponding `Trace` in Tempo.

![Navigate to trace with exemplar](https://user-images.githubusercontent.com/16979322/218629999-1d1cd6ba-2385-4683-975a-d4797df8361a.png)

## References

* [Exemplar specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplar)
* [Exemplars in Prometheus](https://prometheus.io/docs/prometheus/latest/feature_flags/#exemplars-storage)
* [Exemplars in Grafana](https://grafana.com/docs/grafana/latest/fundamentals/exemplars/)
* [Tempo](https://github.com/grafana/tempo)
