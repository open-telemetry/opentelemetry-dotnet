# Prometheus Exporter HttpListener for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Prometheus.HttpListener.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.HttpListener)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Prometheus.HttpListener.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.HttpListener)

An [OpenTelemetry Prometheus exporter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk_exporters/prometheus.md)
that configures an [HttpListener](https://docs.microsoft.com/dotnet/api/system.net.httplistener)
instance for Prometheus to scrape.

## Prerequisite

* [Get Prometheus](https://prometheus.io/docs/introduction/first_steps/)

## Steps to enable OpenTelemetry.Exporter.Prometheus.HttpListener

### Step 1: Install Package

Install

```shell
dotnet add package OpenTelemetry.Exporter.Prometheus.HttpListener
```

### Step 2: Add PrometheusHttpListener

Add and configure `PrometheusHttpListener` with `PrometheusHttpListenerOptions`.

For example:

```csharp
using var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(MyMeter.Name)
    .AddPrometheusHttpListener(
        options => options.Prefixes = new string[] { "http://localhost:9464/" })
    .Build();
```

### Prefixes

Defines the prefixes which will be used by the listener. The default value is `["http://localhost:9464/"]`.
You may specify multiple endpoints.

For details see:
[HttpListenerPrefixCollection.Add(String)](https://docs.microsoft.com/dotnet/api/system.net.httplistenerprefixcollection.add)

### ScrapeEndpointPath

Defines the path for the Prometheus scrape endpoint for by
`UseOpenTelemetryPrometheusScrapingEndpoint`. Default value: `"/metrics"`.

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Exporter-Prometheus" for its internal logging.
Please refer to [SDK
troubleshooting](../OpenTelemetry/README.md#troubleshooting) for instructions on
seeing these internal logs.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
* [Prometheus](https://prometheus.io)
