# Prometheus Exporter HttpListener for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Prometheus.HttpListener.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.HttpListener)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Prometheus.HttpListener.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.HttpListener)

An [OpenTelemetry Prometheus exporter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk_exporters/prometheus.md)
that configures an [HttpListener](https://docs.microsoft.com/dotnet/api/system.net.httplistener)
instance for Prometheus to scrape.

> [!WARNING]
> This component is intended for dev inner-loop, there is no plan to make it
  production ready. Production environments should consider using
  [OpenTelemetry.Exporter.OpenTelemetryProtocol](../OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md).
  Refer to the [Getting Started with Prometheus and
  Grafana](../../docs/metrics/getting-started-prometheus-grafana/README.md)
  tutorial for more information.

## Prerequisite

* [Get Prometheus](https://prometheus.io/docs/introduction/first_steps/)

## Installation

### Step 1: Install Package

```shell
dotnet add package --prerelease OpenTelemetry.Exporter.Prometheus.HttpListener
```

### Step 2: Add PrometheusHttpListener

```csharp
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(MyMeter.Name)
    .AddPrometheusHttpListener()
    .Build();
```

## Configuration

You can configure the `PrometheusHttpListener` through
`PrometheusHttpListenerOptions` and environment variables. The
`PrometheusHttpListenerOptions` setters take precedence over the environment
variables.

### Configuration using Properties

* `Host`: The host used by the Prometheus exporter (default `localhost`).
* `Port`: The port used by the Prometheus exporter (default `9464`).
* `ScrapeEndpointPath`: Defines the Prometheus scrape endpoint path.
  (default `"/metrics"`).
* `DisableTotalNameSuffixForCounters`: Whether to disable the `_total` suffix for
  counter metrics (default `false`).
* `DisableTimestamp`: Whether to disable the timestamp for metrics (default `false`).

### Configuration using Dependency Injection

This exporter allows easy configuration of `PrometheusHttpListenerOptions` from
the dependency injection container, when used in conjunction with
[`OpenTelemetry.Extensions.Hosting`](../OpenTelemetry.Extensions.Hosting/README.md).

For example:

```csharp
var meterProvider = Sdk.CreateMeterProviderBuilder()
    .AddMeter(MyMeter.Name)
    .AddPrometheusHttpListener(options =>
    {
        options.Host = "localhost";
        options.Port = 9464;
    })
    .Build();
```

### Configuration using Environment Variables

The following environment variables can be used to override the default
values of the `PrometheusHttpListenerOptions`.

| Environment variable            | `PrometheusHttpListenerOptions` property |
| --------------------------------| ---------------------------------------- |
| `OTEL_EXPORTER_PROMETHEUS_HOST` | `Host`                                   |
| `OTEL_EXPORTER_PROMETHEUS_PORT` | `Port`                                   |

### ScrapeResponseCacheDurationMilliseconds

Configures scrape endpoint response caching. Multiple scrape requests within the
cache duration time period will receive the same previously generated response.
The default value is `300`. Set to `0` to disable response caching.

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
