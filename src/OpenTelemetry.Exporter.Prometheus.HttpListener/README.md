# Prometheus Exporter HttpListener for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Prometheus.HttpListener.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.HttpListener)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Prometheus.HttpListener.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.HttpListener)

An [OpenTelemetry Prometheus exporter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk_exporters/prometheus.md)
that configures an [HttpListener](https://docs.microsoft.com/dotnet/api/system.net.httplistener)
instance for Prometheus to scrape.

> [!WARNING]
> This component is intended for dev inner-loop, there is no plan to
make it production ready. Production environments should use
[OpenTelemetry.Exporter.Prometheus.AspNetCore](../OpenTelemetry.Exporter.Prometheus.AspNetCore/README.md),
or a combination of
[OpenTelemetry.Exporter.OpenTelemetryProtocol](../OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md)
and [OpenTelemetry
Collector](https://github.com/open-telemetry/opentelemetry-collector).

## Prerequisite

* [Get Prometheus](https://prometheus.io/docs/introduction/first_steps/)

## Steps to enable OpenTelemetry.Exporter.Prometheus.HttpListener

### Step 1: Install Package

```shell
dotnet add package --prerelease OpenTelemetry.Exporter.Prometheus.HttpListener
```

### Step 2: Add PrometheusHttpListener

```csharp
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(MyMeter.Name)
    .AddPrometheusHttpListener(
        options => options.UriPrefixes = new string[] { "http://localhost:9464/" })
    .Build();
```

### UriPrefixes

Defines one or more URI (Uniform Resource Identifier) prefixes which will be
used by the HTTP listener. The default value is `["http://localhost:9464/"]`.

Refer to
[HttpListenerPrefixCollection.Add(String)](https://docs.microsoft.com/dotnet/api/system.net.httplistenerprefixcollection.add)
for more details.

### ScrapeEndpointPath

Defines the Prometheus scrape endpoint path. Default value: `"/metrics"`.

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
