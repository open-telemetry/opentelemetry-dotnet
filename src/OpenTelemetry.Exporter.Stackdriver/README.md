# Stackdriver Exporter for OpenTelemetry .NET

This sample assumes your code authenticates to Stackdriver APIs using [service account][gcp-auth] with
credentials stored in environment variable GOOGLE_APPLICATION_CREDENTIALS.
When you run on [GAE][GAE], [GKE][GKE] or locally with gcloud sdk installed - this is typically the case.
There is also a constructor for specifying path to the service account credential. See [sample][stackdriver-sample] for details.

1. Add [Stackdriver Exporter package][OpenTelemetry-exporter-stackdriver-myget-url] reference.
2. Enable [Stackdriver Trace][stackdriver-trace-setup] API.
3. Enable [Stackdriver Monitoring][stackdriver-monitoring-setup] API.
4. Instantiate a new instance of `StackdriverExporter` with your Google Cloud's ProjectId
5. See [sample][stackdriver-sample] for example use.

### Advanced configuration

You may want to filter on enrich spans and send them to multiple destinations (e.g. for debugging or telemetry self-diagnostics purposes).
You may configure multiple processing pipelines for each destination like shown in below example.

In this example

1. First pipeline sends all sampled in spans to Zipkin
2. Second pipeline sends spans to ApplicationInsights, but filters them first with custom built `FilteringSpanProcessor`
3. Third pipeline adds custom `DebuggingSpanProcessor` that simply logs all calls to debug output

```csharp
using (var tracerFactory = TracerFactory.Create(builder => builder
    .UseZipkin(o =>
    {
        o.Endpoint = new Uri(zipkinUri);
    })
    .UseApplicationInsights(
        o => o.InstrumentationKey = "your-instrumentation-key",
        p => p.AddProcessor(nextProcessor => new FilteringSpanProcessor(nextProcessor)))
    .AddProcessorPipeline(pipelineBuilder => pipelineBuilder.AddProcessor(_ => new DebuggingSpanProcessor()))))
    .SetResource(Resources.CreateServiceResource("test-zipkin"))

{
    // ...
}
```

#### Traces

```csharp
var spanExporter = new StackdriverTraceExporter(projectId);

using var tracerFactory = TracerFactory.Create(builder => builder.AddProcessorPipeline(c => c.SetExporter(spanExporter)));
var tracer = tracerFactory.GetTracer("stackdriver-test");

using (tracer.StartActiveSpan("/getuser", out TelemetrySpan span))
{
    span.AddEvent("Processing video.");
    span.PutHttpMethodAttribute("GET");
    span.PutHttpHostAttribute("localhost", 8080);
    span.PutHttpPathAttribute("/resource");
    span.PutHttpStatusCodeAttribute(200);
    span.PutHttpUserAgentAttribute("Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:47.0) Gecko/20100101 Firefox/47.0");

    Thread.Sleep(TimeSpan.FromMilliseconds(10));
}
```

#### Metrics

```csharp
var metricExporter = new StackdriverExporter(
    "YOUR-GOOGLE-PROJECT-ID",
    Stats.ViewManager);
metricExporter.Start();
```

[OpenTelemetry-exporter-stackdriver-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.Stackdriver.svg
[OpenTelemetry-exporter-stackdriver-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Stackdriver
[stackdriver-trace-setup]: https://cloud.google.com/trace/docs/setup/
[stackdriver-monitoring-setup]: https://cloud.google.com/monitoring/api/enable-api
[GAE]: https://cloud.google.com/appengine/docs/flexible/dotnet/quickstart
[GKE]: https://codelabs.developers.google.com/codelabs/cloud-kubernetes-aspnetcore/index.html?index=..%2F..index#0
[gcp-auth]: https://cloud.google.com/docs/authentication/getting-started
