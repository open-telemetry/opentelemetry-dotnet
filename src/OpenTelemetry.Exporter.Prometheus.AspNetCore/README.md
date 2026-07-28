# Prometheus Exporter AspNetCore for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Prometheus.AspNetCore.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.AspNetCore)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Prometheus.AspNetCore.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus.AspNetCore)

An [OpenTelemetry Prometheus exporter](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk_exporters/prometheus.md)
for configuring an ASP.NET Core application with an endpoint for Prometheus
to scrape.

> [!WARNING]
> This component is still under development due to a dependency on the
  experimental [Prometheus and OpenMetrics
  Compatibility](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/compatibility/prometheus_and_openmetrics.md)
  specification and can undergo breaking changes before stable release.
  Production environments should consider using
  [OpenTelemetry.Exporter.OpenTelemetryProtocol](../OpenTelemetry.Exporter.OpenTelemetryProtocol/README.md).
  Refer to the [Getting Started with Prometheus and
  Grafana](../../docs/metrics/getting-started-prometheus-grafana/README.md)
  tutorial for more information.

<!-- Comment to separate the notes -->

> [!IMPORTANT]
> The Prometheus scraping endpoint is not secured by default, so it is important
> to consider the security implications of exposing this endpoint in your application.
>
> Refer to the
> [Prometheus Security model](https://prometheus.io/docs/operating/security/) and
> [ASP.NET Core security](https://learn.microsoft.com/en-us/aspnet/core/security/)
> documentation for more information and guidance on securing the Prometheus
> scraping endpoint to ensure only authorized users can access the information
> exposed by it.

## Prerequisite

* [Get Prometheus](https://prometheus.io/docs/introduction/first_steps/)

## Steps to enable OpenTelemetry.Exporter.Prometheus.AspNetCore

### Step 1: Install Package

```shell
dotnet add package --prerelease OpenTelemetry.Exporter.Prometheus.AspNetCore
```

### Step 2: Configure OpenTelemetry MeterProvider

* When using
  [OpenTelemetry.Extensions.Hosting](../OpenTelemetry.Extensions.Hosting/README.md)
  package on .NET 6.0+:

    ```csharp
    services.AddOpenTelemetry()
        .WithMetrics(builder => builder.AddPrometheusExporter());
    ```

* Or configure directly:

    Call the `MeterProviderBuilder.AddPrometheusExporter` extension to
    register the Prometheus exporter.

    ```csharp
    var meterProvider = Sdk.CreateMeterProviderBuilder()
        .AddPrometheusExporter()
        .Build();

    builder.Services.AddSingleton(meterProvider);
    ```

### Step 3: Configure Prometheus Scraping Endpoint

You can use register the Prometheus scraping middleware using the
`MapPrometheusScrapingEndpoint` extension method on
`IEndpointRouteBuilder` interface with
[Minimal APIs](https://learn.microsoft.com/aspnet/core/fundamentals/minimal-apis/webapplication).
For example:

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.MapPrometheusScrapingEndpoint();
```

You can use the `IEndpointConventionBuilder` returned by the extension
method to compose with other functionality, such as to exclude HTTP metrics
from the scraping endpoint itself. For example:

```csharp
app.MapPrometheusScrapingEndpoint()
   .DisableHttpMetrics();
```

If you are using the older [Generic Host API](https://learn.microsoft.com/aspnet/core/fundamentals/host/generic-host)
you can register the Prometheus scraping middleware with the
`UseOpenTelemetryPrometheusScrapingEndpoint` extension method on
`IApplicationBuilder` instead:

```csharp
var builder = WebApplication.CreateBuilder(args);

var app = builder.Build();

app.UseOpenTelemetryPrometheusScrapingEndpoint();
```

Overloads of the `UseOpenTelemetryPrometheusScrapingEndpoint` extension are
provided to change the path or for more advanced configuration a predicate
function can be used:

```csharp
app.UseOpenTelemetryPrometheusScrapingEndpoint(
        context => context.Request.Path == "/internal/metrics" &&
                   context.Connection.LocalPort == 5067);
```

This can be used in combination with
[configuring multiple ports on the ASP.NET application](https://learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/endpoints)
to expose the scraping endpoint on a different port.

## Configuration

The `PrometheusExporter` can be configured using the `PrometheusAspNetCoreOptions`
properties.

### ScopeInfoEnabled

Specifies whether metrics include
[scope labels](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/compatibility/prometheus_and_openmetrics.md#instrumentation-scope-1).
Default value: `true`. Set to `false` to disable scope labels.

### ScrapeEndpointPath

Defines the path for the Prometheus scrape endpoint for the middleware
registered by `MapPrometheusScrapingEndpoint` and
`UseOpenTelemetryPrometheusScrapingEndpoint`. Default value: `"/metrics"`.

### ScrapeResponseCacheDurationMilliseconds

Configures scrape endpoint response caching. Multiple scrape requests within the
cache duration time period will receive the same previously generated response.
The default value is `300`. Set to `0` to disable response caching.

### TargetInfoEnabled

Specifies whether to produce a
[`target_info`](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/compatibility/prometheus_and_openmetrics.md#resource-attributes-1)
metric. Default value: `true`. Set to `false` to disable the `target_info` metric.

### ResourceConstantLabels

A predicate used to select which
[resource attributes](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk_exporters/prometheus.md#resource-attributes-as-metric-labels)
are added to each metric as constant labels. The predicate is invoked with the
resource attribute key and should return `true` to include the attribute.
Default value: `null` (no resource attributes are added as metric labels).
Resource attributes copied as metric labels are always included in the
`target_info` metric regardless of this predicate.

```csharp
services.AddOpenTelemetry()
    .WithMetrics(builder => builder
        .AddPrometheusExporter(options =>
        {
            // Add all resource attributes as metric labels.
            options.ResourceConstantLabels = static _ => true;
        }));
```

### TranslationStrategy

Controls how OpenTelemetry metric and label names are translated into Prometheus
names, following the OpenTelemetry specification's `translation_strategy` option.
The strategy combines two independent choices: whether discouraged characters are
escaped to `_` or UTF-8 names are passed through unaltered, and whether unit and
type (e.g. `_total`) suffixes are appended.

| Strategy | Escaping | Suffixes |
| -------- | -------- | -------- |
| `UnderscoreEscapingWithSuffixes` (default) | Escape to `_` | Appended |
| `UnderscoreEscapingWithoutSuffixes` | Escape to `_` | Not appended |
| `NoUTF8EscapingWithSuffixes` | UTF-8 passthrough | Appended |
| `NoTranslation` | UTF-8 passthrough | Not appended |

The escaping choice only sets the default escaping scheme. A scrape request that
negotiates an escaping scheme (via the `escaping` parameter of the `Accept` header,
supported by the version 1.0.0 and later text formats) always takes precedence over
the configured strategy. The classic (pre-1.0.0) text formats do not support
escaping negotiation and are always emitted using underscore escaping; the suffix
choice applies to every format.

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
