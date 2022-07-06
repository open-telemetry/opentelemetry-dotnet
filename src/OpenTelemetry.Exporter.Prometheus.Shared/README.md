# Prometheus Exporter for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Exporter.Prometheus.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Exporter.Prometheus.svg)](https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus)

## Prerequisite

* [Get Prometheus](https://prometheus.io/docs/introduction/first_steps/)

## Steps to enable OpenTelemetry.Exporter.Prometheus

### Step 1: Install Package

Depending on the hosting mechanism you will be using:

Install

```shell
dotnet add package OpenTelemetry.Exporter.Prometheus.AspNetCore
```

or

```shell
dotnet add package OpenTelemetry.Exporter.Prometheus.HttpListener
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

## Configuration

The `PrometheusExporter` can be configured using the `PrometheusExporterOptions`
properties. Refer to
[`TestPrometheusExporter.cs`](../../examples/Console/TestPrometheusExporter.cs)
for example use.

### HttpListener

Configure `HttpListener` with the already constructed meterProvider instance and
passed in `PrometheusHttpListenerOptions` for configuration.
Use `listener.Start()` to allow the listener to start receiving incoming requests.

```csharp
this.listener = new PrometheusHttpListener(
    this.meterProvider,
    o => o.HttpListenerPrefixes = new string[] { "http://localhost:9464/metrics" });
this.listener.Start();
```

#### HttpListenerPrefixes

Defines the prefixes which will be used by the listener. The default value is `["http://localhost:9464/"]`.
You may specify multiple endpoints.

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
