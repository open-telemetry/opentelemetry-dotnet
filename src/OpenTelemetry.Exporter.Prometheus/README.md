# Prometheus Exporter for OpenTelemetry .NET

Configure Prometheus exporter to have stats collected by Prometheus.

1. Get Prometheus using [getting started
   guide](https://prometheus.io/docs/introduction/first_steps/).
2. Start `PrometheusExporter` as below.
3. See
   [sample](https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/Console/TestPrometheus.cs)
   for example use.

```csharp
var exporter = new PrometheusExporter(
    new PrometheusExporterOptions()
    {
        Url = "http://+:9184/metrics/"
    },
    Stats.ViewManager);

exporter.Start();

try
{
    // record metrics
    statsRecorder.NewMeasureMap().Put(VideoSize, values[0] * MiB).Record();
}
finally
{
    exporter.Stop();
}
```
