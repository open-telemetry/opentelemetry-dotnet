# OpenCensus .NET SDK - distributed tracing and stats collection framework

[![Gitter chat][gitter-image]][gitter-url]
[![Build Status](https://opencensus.visualstudio.com/continuous-integration/_apis/build/status/ci-myget-update.yml)](https://opencensus.visualstudio.com/continuous-integration/_build/latest?definitionId=3)

OpenCensus is a toolkit for collecting application performance and behavior
data. It currently includes 3 APIs: stats, tracing and tags.

The library is in [Beta](#versioning) stage and APIs are expected to be mostly
stable. The library is expected to move to [GA](#versioning) stage after v1.0.0
major release.

Please join [gitter](https://gitter.im/census-instrumentation/Lobby) for help
or feedback on this project.

We encourage contributions. Use tags [up-for-grabs][up-for-grabs-issues] and
[good first issue][good-first-issues] to get started with the project. Follow
[CONTRIBUTING](CONTRIBUTING.md) guide to report issues or submit a proposal.

## Packages

### API and implementation

| Package                 | MyGet (CI)       | NuGet (releases) |
| ----------------------- | ---------------- | -----------------|
| OpenCensus              | [![MyGet Nightly][opencensus-myget-image]][opencensus-myget-url]         | [![NuGet Release][opencensus-nuget-image]][opencensus-nuget-url]         |
| OpenCensus.Abstractions | [![MyGet Nightly][opencensus-abs-myget-image]][opencensus-abs-myget-url] | [![NuGet Release][opencensus-abs-nuget-image]][opencensus-abs-nuget-url] |

### Data Collectors

| Package                 | MyGet (CI)       | NuGet (releases) |
| ----------------------- | ---------------- | -----------------|
| Asp.Net Core            | [![MyGet Nightly][opencensus-collect-aspnetcore-myget-image]][opencensus-collect-aspnetcore-myget-url]       | [![NuGet Release][opencensus-collect-aspnetcore-nuget-image]][opencensus-collect-aspnetcore-nuget-url]   |
| .Net Core HttpClient    | [![MyGet Nightly][opencensus-collect-deps-myget-image]][opencensus-collect-deps-myget-url]                   | [![NuGet Release][opencensus-collect-deps-nuget-image]][opencensus-collect-deps-nuget-url]               |
| StackExchange.Redis     | [![MyGet Nightly][opencensus-collect-stackexchange-redis-myget-image]][opencensus-collect-stackexchange-redis-myget-url]    | [![NuGet Release][opencensus-collect-stackexchange-redis-nuget-image]][opencensus-collect-stackexchange-redis-nuget-url]|

### Exporters Packages

| Package                 | MyGet (CI)       | NuGet (releases) |
| ----------------------- | ---------------- | -----------------|
| Zipkin                  | [![MyGet Nightly][opencensus-exporter-zipkin-myget-image]][opencensus-exporter-zipkin-myget-url]            | [![NuGet release][opencensus-exporter-zipkin-nuget-image]][opencensus-exporter-zipkin-nuget-url]            |
| Prometheus              | [![MyGet Nightly][opencensus-exporter-prom-myget-image]][opencensus-exporter-prom-myget-url]                | [![NuGet release][opencensus-exporter-prom-nuget-image]][opencensus-exporter-prom-nuget-url]                |
| Application Insights    | [![MyGet Nightly][opencensus-exporter-ai-myget-image]][opencensus-exporter-ai-myget-url]                    | [![NuGet release][opencensus-exporter-ai-nuget-image]][opencensus-exporter-ai-nuget-url]                    |
| Stackdriver             | [![MyGet Nightly][opencensus-exporter-stackdriver-myget-image]][opencensus-exporter-stackdriver-myget-url]  | [![NuGet release][opencensus-exporter-stackdriver-nuget-image]][opencensus-exporter-stackdriver-nuget-url]  |

## OpenCensus QuickStart: collecting data

You can use Open Census API to instrument code and report data. Or use one of
automatic data collection modules.

### Using ASP.NET Core incoming requests collector

Incoming requests of ASP.NET Core app can be automatically tracked.

1. Install packages to your project:
   [OpenCensus][opencensus-nuget-url]
   [OpenCensus.Collector.AspNetCore][opencensus-collect-aspnetcore-nuget-url]

2. Make sure `ITracer`, `ISampler`, and `IPropagationComponent` registered in DI.
    ``` csharp
    services.AddSingleton<ITracer>(Tracing.Tracer);
    services.AddSingleton<ISampler>(Samplers.AlwaysSample);
    services.AddSingleton<IPropagationComponent>(new DefaultPropagationComponent());
    ```

3. Configure data collection singletons in ConfigureServices method:
    ``` csharp
    public void ConfigureServices(IServiceCollection services)
    {
        // ...
        services.AddSingleton<RequestsCollectorOptions>(new RequestsCollectorOptions());
        services.AddSingleton<RequestsCollector>();
    ```

4. Initialize data collection by instantiating singleton in Configure method
    ``` csharp
    public void Configure(IApplicationBuilder app, /*... other arguments*/ )
    {
        // ...
        var collector = app.ApplicationServices.GetService<RequestsCollector>();
    ```

### Using Dependencies collector

Outgoing http calls made by .NET Core `HttpClient` can be automatically tracked.

1. Install package to your project:
   [OpenCensus.Collector.Dependencies][opencensus-collect-deps-nuget-url]

2. Make sure `ITracer`, `ISampler`, and `IPropagationComponent` registered in DI.
    ``` csharp
    services.AddSingleton<ITracer>(Tracing.Tracer);
    services.AddSingleton<ISampler>(Samplers.AlwaysSample);
    services.AddSingleton<IPropagationComponent>(new DefaultPropagationComponent());
    ```

3. Configure data collection singletons in ConfigureServices method:
    ``` csharp
    public void ConfigureServices(IServiceCollection services)
    {
        // ...
        services.AddSingleton<DependenciesCollectorOptions>(new DependenciesCollectorOptions());
        services.AddSingleton<DependenciesCollector>();
    ```

4. Initiate data collection by instantiating singleton in Configure method
    ``` csharp
    public void Configure(IApplicationBuilder app, /*... other arguments*/ )
    {
        // ...
        var depCollector = app.ApplicationServices.GetService<DependenciesCollector>();
    ```

### Using StackExchange.Redis collector

Outgoing http calls to Redis made usign StackExchange.Redis library can be automatically tracked.

1. Install package to your project:
   [OpenCensus.Collector.StackExchangeRedis][opencensus-collect-stackexchange-redis-nuget-url]

2. Make sure `ITracer`, `ISampler`, and `IExportComponent` registered in DI.
    ``` csharp
    services.AddSingleton<ITracer>(Tracing.Tracer);
    services.AddSingleton<ISampler>(Samplers.AlwaysSample);
    services.AddSingleton<IExportComponent>(Tracing.ExportComponent);
    ```

3. Configure data collection singletons in ConfigureServices method:
    ``` csharp
    public void ConfigureServices(IServiceCollection services)
    {
        // ...
        services.AddSingleton<StackExchangeRedisCallsCollectorOptions>(new StackExchangeRedisCallsCollectorOptions());
        services.AddSingleton<StackExchangeRedisCallsCollector>();
    ```

4. Initiate data collection by instantiating singleton in Configure method
    ``` csharp
    public void Configure(IApplicationBuilder app, /*... other arguments*/ )
    {
        // ...
        var redisCollector = app.ApplicationServices.GetService<StackExchangeRedisCallsCollector>();

        // use collector to configure the profiler
        ConnectionMultiplexer connection = ConnectionMultiplexer.Connect("localhost:6379");
        connection.RegisterProfiler(redisCollector.GetProfilerSessionsFactory());
    ```

## OpenCensus QuickStart: exporting data

### Using Zipkin exporter

Configure Zipkin exporter to see traces in Zipkin UI.

1. Get Zipkin using [getting started guide][zipkin-get-started].
2. Start `ZipkinTraceExporter` as below:
3. See [sample][zipkin-sample] for example use.

``` csharp
var exporter = new ZipkinTraceExporter(
  new ZipkinTraceExporterOptions() {
    Endpoint = new Uri("https://<zipkin-server:9411>/api/v2/spans"),
    ServiceName = typeof(Program).Assembly.GetName().Name,
  },
  Tracing.ExportComponent);
exporter.Start();

var span = tracer
            .SpanBuilder("incoming request")
            .SetSampler(Samplers.AlwaysSample)
            .StartSpan();

Thread.Sleep(TimeSpan.FromSeconds(1));
span.End();
```

### Using Prometheus exporter

Configure Prometheus exporter to have stats collected by Prometheus.

1. Get Prometheus using [getting started guide][prometheus-get-started].
2. Start `PrometheusExporter` as below.
3. See [sample][prometheus-sample] for example use.

``` csharp
var exporter = new PrometheusExporter(
    new PrometheusExporterOptions()
    {
        Url = new Uri("http://localhost:9184/metrics/")
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

### Using Stackdriver Exporter

This sample assumes your code authenticates to Stackdriver APIs using [service account][gcp-auth] with
credentials stored in environment variable GOOGLE_APPLICATION_CREDENTIALS.
When you run on [GAE][GAE], [GKE][GKE] or locally with gcloud sdk installed - this is typically the case.
There is also a constructor for specifying path to the service account credential. See [sample][stackdriver-sample] for details.

1. Add [Stackdriver Exporter package][opencensus-exporter-stackdriver-myget-url] reference.
2. Enable [Stackdriver Trace][stackdriver-trace-setup] API.
3. Enable [Stackdriver Monitoring][stackdriver-monitoring-setup] API.
4. Instantiate a new instance of `StackdriverExporter` with your Google Cloud's ProjectId
5. See [sample][stackdriver-sample] for example use.

``` csharp
    var exporter = new StackdriverExporter(
        "YOUR-GOOGLE-PROJECT-ID",
        Tracing.ExportComponent,
        Stats.ViewManager);
    exporter.Start();
```

### Using Application Insights exporter

1. Create [Application Insights][ai-get-started] resource.
2. Set instrumentation key via telemetry configuration object
   (`new TelemetryConfiguration("iKey")`). This object may be injected via
   dependency injection as well.
3. Instantiate a new instance of `ApplicationInsightsExporter`.
4. See [sample][ai-sample] for example use.

``` csharp
var config = new TelemetryConfiguration("iKey")
var exporter = new ApplicationInsightsExporter(
    Tracing.ExportComponent,
    Stats.ViewManager,
    config); // either global or local config can be used
exporter.Start();
```

## Versioning
  
This library follows [Semantic Versioning][semver].
  
**GA**: Libraries defined at a GA quality level are stable, and will not
introduce backwards-incompatible changes in any minor or patch releases. We
will address issues and requests with the highest priority. If we were to make
a backwards-incompatible changes on an API, we will first mark the existing API
as deprecated and keep it for 18 months before removing it.
  
**Beta**: Libraries defined at a Beta quality level are expected to be mostly
stable and we're working towards their release candidate. We will address
issues and requests with a higher priority. There may be backwards incompatible
changes in a minor version release, though not in a patch release. If an
element is part of an API that is only meant to be used by exporters or other
opencensus libraries, then there is no deprecation period. Otherwise, we will
deprecate it for 18 months before removing it, if possible.

[gitter-image]: https://badges.gitter.im/census-instrumentation/lobby.svg
[gitter-url]:https://gitter.im/census-instrumentation/lobby?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge
[opencensus-myget-image]:https://img.shields.io/myget/opencensus/vpre/OpenCensus.svg
[opencensus-myget-url]: https://www.myget.org/feed/opencensus/package/nuget/OpenCensus
[opencensus-abs-myget-image]:https://img.shields.io/myget/opencensus/vpre/OpenCensus.Abstractions.svg
[opencensus-abs-myget-url]: https://www.myget.org/feed/opencensus/package/nuget/OpenCensus.Abstractions
[opencensus-exporter-zipkin-myget-image]:https://img.shields.io/myget/opencensus/vpre/OpenCensus.Exporter.Zipkin.svg
[opencensus-exporter-zipkin-myget-url]: https://www.myget.org/feed/opencensus/package/nuget/OpenCensus.Exporter.Zipkin
[opencensus-exporter-prom-myget-image]:https://img.shields.io/myget/opencensus/vpre/OpenCensus.Exporter.Prometheus.svg
[opencensus-exporter-prom-myget-url]: https://www.myget.org/feed/opencensus/package/nuget/OpenCensus.Exporter.Prometheus
[opencensus-exporter-ai-myget-image]:https://img.shields.io/myget/opencensus/vpre/OpenCensus.Exporter.ApplicationInsights.svg
[opencensus-exporter-ai-myget-url]: https://www.myget.org/feed/opencensus/package/nuget/OpenCensus.Exporter.ApplicationInsights
[opencensus-exporter-stackdriver-myget-image]:https://img.shields.io/myget/opencensus/vpre/OpenCensus.Exporter.Stackdriver.svg
[opencensus-exporter-stackdriver-myget-url]: https://www.myget.org/feed/opencensus/package/nuget/OpenCensus.Exporter.Stackdriver
[opencensus-collect-aspnetcore-myget-image]:https://img.shields.io/myget/opencensus/vpre/OpenCensus.Collector.AspNetCore.svg
[opencensus-collect-aspnetcore-myget-url]: https://www.myget.org/feed/opencensus/package/nuget/OpenCensus.Collector.AspNetCore
[opencensus-collect-deps-myget-image]:https://img.shields.io/myget/opencensus/vpre/OpenCensus.Collector.Dependencies.svg
[opencensus-collect-deps-myget-url]: https://www.myget.org/feed/opencensus/package/nuget/OpenCensus.Collector.Dependencies
[opencensus-collect-stackexchange-redis-myget-image]:https://img.shields.io/myget/opencensus/vpre/OpenCensus.Collector.StackExchangeRedis.svg
[opencensus-collect-stackexchange-redis-myget-url]: https://www.myget.org/feed/opencensus/package/nuget/OpenCensus.Collector.StackExchangeRedis
[opencensus-nuget-image]:https://img.shields.io/nuget/vpre/OpenCensus.svg
[opencensus-nuget-url]:https://www.nuget.org/packages/OpenCensus
[opencensus-abs-nuget-image]:https://img.shields.io/nuget/vpre/OpenCensus.Abstractions.svg
[opencensus-abs-nuget-url]: https://www.nuget.org/packages/OpenCensus.Abstractions
[opencensus-exporter-zipkin-nuget-image]:https://img.shields.io/nuget/vpre/OpenCensus.Exporter.Zipkin.svg
[opencensus-exporter-zipkin-nuget-url]: https://www.nuget.org/packages/OpenCensus.Exporter.Zipkin
[opencensus-exporter-prom-nuget-image]:https://img.shields.io/nuget/vpre/OpenCensus.Exporter.Prometheus.svg
[opencensus-exporter-prom-nuget-url]: https://www.nuget.org/packages/OpenCensus.Exporter.Prometheus
[opencensus-exporter-ai-nuget-image]:https://img.shields.io/nuget/vpre/OpenCensus.Exporter.ApplicationInsights.svg
[opencensus-exporter-ai-nuget-url]: https://www.nuget.org/packages/OpenCensus.Exporter.ApplicationInsights
[opencensus-exporter-stackdriver-nuget-image]:https://img.shields.io/nuget/vpre/OpenCensus.Exporter.Stackdriver.svg
[opencensus-exporter-stackdriver-nuget-url]: https://www.nuget.org/packages/OpenCensus.Exporter.Stackdriver
[opencensus-collect-aspnetcore-nuget-image]:https://img.shields.io/nuget/vpre/OpenCensus.Collector.AspNetCore.svg
[opencensus-collect-aspnetcore-nuget-url]: https://www.nuget.org/packages/OpenCensus.Collector.AspNetCore
[opencensus-collect-deps-nuget-image]:https://img.shields.io/nuget/vpre/OpenCensus.Collector.Dependencies.svg
[opencensus-collect-deps-nuget-url]: https://www.nuget.org/packages/OpenCensus.Collector.Dependencies
[opencensus-collect-stackexchange-redis-nuget-image]:https://img.shields.io/nuget/vpre/OpenCensus.Collector.StackExchangeRedis.svg
[opencensus-collect-stackexchange-redis-nuget-url]: https://www.nuget.org/packages/OpenCensus.Collector.StackExchangeRedis
[up-for-grabs-issues]: https://github.com/census-instrumentation/opencensus-csharp/issues?q=is%3Aissue+is%3Aopen+label%3Aup-for-grabs
[good-first-issues]: https://github.com/census-instrumentation/opencensus-csharp/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22
[zipkin-get-started]: https://zipkin.io/pages/quickstart.html
[ai-get-started]: https://docs.microsoft.com/azure/application-insights
[stackdriver-trace-setup]: https://cloud.google.com/trace/docs/setup/
[stackdriver-monitoring-setup]: https://cloud.google.com/monitoring/api/enable-api
[GAE]: https://cloud.google.com/appengine/docs/flexible/dotnet/quickstart
[GKE]: https://codelabs.developers.google.com/codelabs/cloud-kubernetes-aspnetcore/index.html?index=..%2F..index#0
[gcp-auth]: https://cloud.google.com/docs/authentication/getting-started
[semver]: http://semver.org/
[ai-sample]: https://github.com/census-instrumentation/opencensus-csharp/blob/develop/src/Samples/TestApplicationInsights.cs
[stackdriver-sample]: https://github.com/census-instrumentation/opencensus-csharp/blob/develop/src/Samples/TestStackdriver.cs
[zipkin-sample]: https://github.com/census-instrumentation/opencensus-csharp/blob/develop/src/Samples/TestZipkin.cs
[prometheus-get-started]: https://prometheus.io/docs/introduction/first_steps/
[prometheus-sample]: https://github.com/census-instrumentation/opencensus-csharp/blob/develop/src/Samples/TestPrometheus.cs