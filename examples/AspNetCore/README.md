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

## Export metrics to Prometheus

Switch to Prometheus by setting `UseMetricsExporter` to `"prometheus"` in `appsettings.json`.

Follow [guide](/docs/metrics/getting-started-prometheus-grafana/README.md#collect-metrics-using-prometheus)
on how to set up prometheus.

Note that for this example, the `yml` file configuration needs an additional
parameter `scheme` for https. Sample configuration:

```yaml
global:
  scrape_interval: 10s
  evaluation_interval: 10s
scrape_configs:
  - job_name: "otel"
    scheme: https
    static_configs:
      - targets: ["localhost:53750"]
```

See the metrics at [https://localhost:53750/metrics](https://localhost:53750/metrics).

Make a request at [https://localhost:53750/WeatherForecast](https://localhost:53750/WeatherForecast)
for HTTP metrics.

## References

* [ASP.NET Core 3.1 Example](https://github.com/open-telemetry/opentelemetry-dotnet/tree/98cb28974af43fc893ab80a8cead6e2d4163e144/examples/AspNetCore)
* [OpenTelemetry Project](https://opentelemetry.io/)
