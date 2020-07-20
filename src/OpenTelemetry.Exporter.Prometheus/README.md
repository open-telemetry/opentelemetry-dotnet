# Prometheus Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Prometheus.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Prometheus.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus)

## Prerequisite

* [Get Prometheus](https://prometheus.io/docs/introduction/first_steps/)

## Installation

```shell
dotnet add package OpenTelemetry.Exporter.Prometheus
```

## Configuration

Start `PrometheusExporter` as below.

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

See
[sample](https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/Console/TestPrometheus.cs)
for example use.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Prometheus](https://prometheus.io)
