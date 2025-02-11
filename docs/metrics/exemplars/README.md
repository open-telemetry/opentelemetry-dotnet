# Using Exemplars in OpenTelemetry .NET

<details>
<summary>Table of Contents</summary>

* [Install and run Jaeger](#install-and-run-jaeger)
* [Install and run Prometheus](#install-and-run-prometheus)
* [Install and configure Grafana](#install-and-configure-grafana)
* [Export metrics and traces from the
  application](#export-metrics-and-traces-from-the-application)
* [Use exemplars to navigate from metrics to
  traces](#use-exemplars-to-navigate-from-metrics-to-traces)
* [Learn more](#learn-more)

</details>

[Exemplars](../customizing-the-sdk/README.md#exemplars) are example data points
for aggregated data. They provide specific context to otherwise general
aggregations. One common use case is to gain ability to correlate metrics to
traces (and logs). While OpenTelemetry .NET supports Exemplars, it is only
useful if the telemetry backend also supports the capabilities. This tutorial
uses well known open-source backends to demonstrate the concept. The following
components are involved:

* [Program.cs](./Program.cs) - this application is instrumented with
  OpenTelemetry, it sends metrics to Prometheus, and traces to Jaeger.
* [Prometheus](#install-and-run-prometheus) - Prometheus is used as the metrics
  backend.
* [Jaeger](#install-and-run-jaeger) - Jaeger is used as the distributed tracing
  backend.
* [Grafana](#install-and-configure-grafana) - UI to query metrics from
  Prometheus, traces from Jaeger, and to navigate between metrics and traces
  using Exemplars.

## Install and run Jaeger

Download the [latest binary distribution
archive](https://www.jaegertracing.io/download/) of Jaeger.

After finished downloading, extract it to a local location that's easy to
access. Run the `jaeger-all-in-one(.exe)` executable:

```sh
./jaeger-all-in-one --collector.otlp.enabled
```

## Install and run Prometheus

Follow the [first steps](https://prometheus.io/docs/introduction/first_steps/)
to download the [latest release](https://prometheus.io/download/) of Prometheus.

After finished downloading, extract it to a local location that's easy to
access. Run the `prometheus(.exe)` server executable with feature flags
[exemplars
storage](https://prometheus.io/docs/prometheus/latest/feature_flags/#exemplars-storage)
and
[otlp-receiver](https://prometheus.io/docs/prometheus/latest/feature_flags/#otlp-receiver)
enabled:

```sh
./prometheus --enable-feature=exemplar-storage --web.enable-otlp-receiver
```

## Install and configure Grafana

Follow the operating system specific instructions to [download and install
Grafana](https://grafana.com/docs/grafana/latest/setup-grafana/installation/#supported-operating-systems).

After installation, start the standalone Grafana server (`grafana-server.exe` or
`./bin/grafana-server`, depending on the operating system). Then, use a
[supported web
browser](https://grafana.com/docs/grafana/latest/setup-grafana/installation/#supported-web-browsers)
to navigate to [http://localhost:3000/](http://localhost:3000/).

Follow the instructions in the Grafana getting started
[doc](https://grafana.com/docs/grafana/latest/getting-started/getting-started/#step-2-log-in)
to log in.

After successfully logging in, hover on the Configuration icon
on the panel at the left hand side, and click on Plugins.

Find and click on the Jaeger plugin. Next click on `Create a Jaeger data source`
button. Make the following changes:

1. Set "URL" to `http://localhost:16686/`.
2. At the bottom of the page click `Save & test` to ensure the data source is
   working.

![Add Jaeger data
source](https://github.com/open-telemetry/opentelemetry-dotnet/assets/17327289/8356dc1d-dad2-4c82-9936-9a84b51d12fa)

Find and click on the Prometheus plugin. Next click on
`Create a Prometheus data source` button. Make the following changes:

1. Set "URL" to `http://localhost:9090`.
2. Under the "Exemplars" section, enable "Internal link", set "Data source" to
   `Jaeger`, and set "Label name" to `trace_id`.
3. At the bottom of the page click `Save & test` to ensure the data source is
   working.

![Add Prometheus data
source](https://github.com/open-telemetry/opentelemetry-dotnet/assets/17327289/a137c4ac-dfd7-4d24-8811-208f66e67e37)

## Export metrics and traces from the application

Create a new console application and run it:

```sh
dotnet new console --output exemplars
cd exemplars
dotnet run
```

Add reference to [OTLP
Exporter](../../../src/OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md):

```sh
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Now copy the code from [Program.cs](./Program.cs) and run the application again.
The application will start sending metrics to Prometheus and traces to Jaeger.

The application is configured with trace-based exemplar filter, which enables
the OpenTelemetry SDK to attach exemplars to metrics:

```csharp
var meterProvider = Sdk.CreateMeterProviderBuilder()
    ...
    .SetExemplarFilter(ExemplarFilterType.TraceBased)
    ...
```

For more details about the `SetExemplarFilter` API see: [Customizing
OpenTelemetry .NET SDK for Metrics >
ExemplarFilter](../customizing-the-sdk/README.md#exemplarfilter).

## Use exemplars to navigate from metrics to traces

Open Grafana, select Explore, and select Prometheus as the source. Select the
metric named `MyHistogram_bucket`, and plot the chart. Toggle on the "Exemplars"
option from the UI and hit refresh.

![Enable
Exemplars](https://github.com/open-telemetry/opentelemetry-dotnet/assets/17327289/bc461c6d-a0b9-49b7-a91d-94b07c3f417f)

The Exemplars appear as special "diamond shaped dots" along with the metric
charts in the UI. Select any exemplar to see the exemplar data, which includes
the timestamp when the measurement was recorded, the raw value, and trace
context when the recording was done. The "trace_id" enables jumping to the
tracing backed (Jaeger in this case). Click on the "Query with Jaeger" button
next to the "trace_id" field to open the corresponding trace in Jaeger.

![Navigate to trace with
exemplar](https://github.com/open-telemetry/opentelemetry-dotnet/assets/17327289/56bb5297-f744-41f3-bc35-8596392b8673)

## Learn more

* [Exemplar
  specification](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#exemplar)
* [What is Prometheus?](https://prometheus.io/docs/introduction/overview/)
* [Prometheus now supports OpenTelemetry
  Metrics](https://horovits.medium.com/prometheus-now-supports-opentelemetry-metrics-83f85878e46a)
* [Jaeger Tracing](https://www.jaegertracing.io/)
* [Grafana support for
  Prometheus](https://prometheus.io/docs/visualization/grafana/#creating-a-prometheus-graph)
