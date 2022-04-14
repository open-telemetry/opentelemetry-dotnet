# Prometheus Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Prometheus.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Prometheus.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus)

## Prerequisite

* [Get Prometheus](https://prometheus.io/docs/introduction/first_steps/)

## Steps to enable OpenTelemetry.Exporter.Prometheus

### Step 1: Install Package

```shell
dotnet add package OpenTelemetry.Exporter.Prometheus
```

### Step 2: Configure OpenTelemetry MeterProvider

* When using OpenTelemetry.Extensions.Hosting package on .NET Core 3.1+:

    ```csharp
    services.AddOpenTelemetryMetrics(builder =>
    {
        builder.AddPrometheusExporter();
    });
    ```

* Or configure directly:

    Call the `AddPrometheusExporter` `MeterProviderBuilder` extension to
    register the Prometheus exporter.

    ```csharp
    using var meterProvider = Sdk.CreateMeterProviderBuilder()
        .AddPrometheusExporter()
        .Build();
    ```

### Step 3: Configure Prometheus Scraping Endpoint

* On .NET Core 3.1+ register Prometheus scraping middleware using the
  `UseOpenTelemetryPrometheusScrapingEndpoint` extension:

    ```csharp
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint();
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
    ```

    Overloads of the `UseOpenTelemetryPrometheusScrapingEndpoint` extension are
    provided to change the path or for more advanced configuration a predicate
    function can be used:

    ```csharp
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseOpenTelemetryPrometheusScrapingEndpoint(
            context => context.Request.Path == "/internal/metrics"
                && context.Connection.LocalPort == 5067);
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });
    }
    ```

* On .NET Framework an HTTP listener is automatically started which will respond
  to scraping requests. See the [Configuration](#configuration) section for
  details on the settings available. This may also be turned on in .NET Core (it
  is OFF by default) when the ASP.NET Core pipeline is not available for
  middleware registration.

## Configuration

The `PrometheusExporter` can be configured using the `PrometheusExporterOptions`
properties. Refer to
[`TestPrometheusExporter.cs`](../../examples/Console/TestPrometheusExporter.cs)
for example use.

### StartHttpListener

Set to `true` to start an HTTP listener which will respond to Prometheus scrape
requests using the [HttpListenerPrefixes](#httplistenerprefixes) and
[ScrapeEndpointPath](#scrapeendpointpath) options.

Defaults:

* On .NET Framework this is `true` by default.

* On .NET Core 3.1+ this is `false` by default. Users running ASP.NET Core
  should use the `UseOpenTelemetryPrometheusScrapingEndpoint` extension to
  register the scraping middleware instead of using the listener.

### HttpListenerPrefixes

Defines the prefixes which will be used by the listener when `StartHttpListener`
is `true`. The default value is `["http://localhost:9464/"]`. You may specify
multiple endpoints.

For details see:
[HttpListenerPrefixCollection.Add(String)](https://docs.microsoft.com/dotnet/api/system.net.httplistenerprefixcollection.add)

### ScrapeEndpointPath

Defines the path for the Prometheus scrape endpoint for
either the HTTP listener or the middleware registered by
`UseOpenTelemetryPrometheusScrapingEndpoint`. Default value: `"/metrics"`.

### ScrapeResponseCacheDurationMilliseconds

Configures scrape endpoint response caching. Multiple scrape requests within the
cache duration time period will receive the same previously generated response.
The default value is `10000` (10 seconds). Set to `0` to disable response
caching.

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
