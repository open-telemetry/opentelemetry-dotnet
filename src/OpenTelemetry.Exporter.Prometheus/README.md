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

You can configure the `PrometheusExporter` by following the directions below:

* `Url`: The url to listen to. Typically it ends with `/metrics` like `http://localhost:9184/metrics/`.

See
[`TestPrometheusExporter.cs`](../../examples/Console/TestPrometheusExporter.cs)
for example use.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Prometheus](https://prometheus.io)
