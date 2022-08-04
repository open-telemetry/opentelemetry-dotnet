# OpenTelemetry ASP.Net Core 6  Web API Example

This example uses the new WebApplication host that ships with .Net 6
and shows how to setup

1. OpenTelemetry logging
2. OpenTelemetry metrics
3. OpenTelemetry tracing

`ResourceBuilder` is associated with OpenTelemetry to associate the
service name, version and the machine on which this program is running.

The sample rate is set to emit all the traces using `AlwaysOnSampler`.
You can try out different samplers like `TraceIdRatioBasedSampler`.

## How to run the example

The example creates a `WeatherForecast` API. To generate telemetry including HTTP
metrics, make a request at `https://localhost:<port>/WeatherForecast`.

Note: `<port>` is a randomly chosen port number. It is generated when running
the example for the first time according to the
[tutorial](https://docs.microsoft.com/aspnet/core/tutorials/first-web-api#test-the-project).

## Export metrics to Prometheus

Switch to Prometheus by setting `UseMetricsExporter` to `"prometheus"` in `appsettings.json`.

Follow this [guide](../../docs/metrics/getting-started-prometheus-grafana/README.md#collect-metrics-using-prometheus)
to set up Prometheus.

Note that for this example, the `yml` file configuration needs an additional
parameter `scheme` for HTTPS. Here goes an example:

```yaml
global:
  scrape_interval: 10s
  evaluation_interval: 10s
scrape_configs:
  - job_name: "otel"
    scheme: https
    static_configs:
      - targets: ["localhost:<port>"]
```

Start the example project and Prometheus and keep them running.
Now you should be able to see the metrics at `https://localhost:<port>/metrics`.

## References

* [ASP.NET Core 3.1 Example](https://github.com/open-telemetry/opentelemetry-dotnet/tree/98cb28974af43fc893ab80a8cead6e2d4163e144/examples/AspNetCore)
* [OpenTelemetry Project](https://opentelemetry.io/)
