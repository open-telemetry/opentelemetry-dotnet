# OpenTelemetry .NET SDK - distributed tracing and stats collection framework

.NET Channel: [![Gitter chat][dotnet-gitter-image]][dotnet-gitter-url]

Community Channel: [![Gitter chat][main-gitter-image]][main-gitter-url]

We hold regular meetings. See details at [community page](https://github.com/open-telemetry/community#net-sdk).

Approvers ([@open-telemetry/dotnet-approvers](https://github.com/orgs/open-telemetry/teams/dotnet-approvers)):

- [Bruno Garcia](https://github.com/bruno-garcia), Sentry
- [Christoph Neumueller](https://github.com/discostu105), Dynatrace
- [Liudmila Molkova](https://github.com/lmolkova), Microsoft
- [Mike Goldsmith](https://github.com/MikeGoldsmith), LightStep

*Find more about the approver role in [community repository](https://github.com/open-telemetry/community/blob/master/community-membership.md#approver).*

Maintainers ([@open-telemetry/dotnet-maintainers](https://github.com/orgs/open-telemetry/teams/dotnet-maintainers)):

- [Austin Parker](https://github.com/austinlparker), LightStep
- [Sergey Kanzhelev](https://github.com/SergeyKanzhelev), Microsoft

*Find more about the maintainer role in [community repository](https://github.com/open-telemetry/community/blob/master/community-membership.md#maintainer).*

[![Build Status](https://dev.azure.com/opentelemetry/pipelines/_apis/build/status/open-telemetry.opentelemetry-dotnet-myget-update?branchName=master)](https://dev.azure.com/opentelemetry/pipelines/_build/latest?definitionId=2&branchName=master)

OpenTelemetry is a toolkit for collecting application performance and behavior
data.

The library is in [Alpha](#versioning) stage. The library is expected to move
to [GA](#versioning) stage after v1.0.0 major release.

Please join [gitter][dotnet-gitter-url] for help or feedback on this project.

We encourage contributions. Use tags [up-for-grabs][up-for-grabs-issues] and
[good first issue][good-first-issues] to get started with the project. Follow
[CONTRIBUTING](CONTRIBUTING.md) guide to report issues or submit a proposal.

## Packages

### Nightly builds

Myget feeds:

- NuGet V3 feed: https://www.myget.org/F/opentelemetry/api/v3/index.json
- NuGet V2 feed: https://www.myget.org/F/opentelemetry/api/v2 

### API and implementation

| Package                    | MyGet (CI)                                                                     | NuGet (releases)                                                               |
| -------------------------- | ------------------------------------------------------------------------------ | ------------------------------------------------------------------------------ |
| OpenTelemetry              | [![MyGet Nightly][OpenTelemetry-myget-image]][OpenTelemetry-myget-url]         | [![NuGet Release][OpenTelemetry-nuget-image]][OpenTelemetry-nuget-url]         |
| OpenTelemetry.Api | [![MyGet Nightly][OpenTelemetry-abs-myget-image]][OpenTelemetry-abs-myget-url] | [![NuGet Release][OpenTelemetry-abs-nuget-image]][OpenTelemetry-abs-nuget-url] |

### Data Collectors

| Package                           | MyGet (CI)                                                                                                                     | NuGet (releases)                                                                                                               |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------ |
| ASP.NET Core                      | [![MyGet Nightly][OpenTelemetry-collect-aspnetcore-myget-image]][OpenTelemetry-collect-aspnetcore-myget-url]                   | [![NuGet Release][OpenTelemetry-collect-aspnetcore-nuget-image]][OpenTelemetry-collect-aspnetcore-nuget-url]                   |
| .NET Core HttpClient & Azure SDKs | [![MyGet Nightly][OpenTelemetry-collect-deps-myget-image]][OpenTelemetry-collect-deps-myget-url]                               | [![NuGet Release][OpenTelemetry-collect-deps-nuget-image]][OpenTelemetry-collect-deps-nuget-url]                               |
| StackExchange.Redis               | [![MyGet Nightly][OpenTelemetry-collect-stackexchange-redis-myget-image]][OpenTelemetry-collect-stackexchange-redis-myget-url] | [![NuGet Release][OpenTelemetry-collect-stackexchange-redis-nuget-image]][OpenTelemetry-collect-stackexchange-redis-nuget-url] |

### Exporters Packages

| Package              | MyGet (CI)                                                                                                       | NuGet (releases)                                                                                                 |
| -------------------- | ---------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Zipkin               | [![MyGet Nightly][OpenTelemetry-exporter-zipkin-myget-image]][OpenTelemetry-exporter-zipkin-myget-url]           | [![NuGet release][OpenTelemetry-exporter-zipkin-nuget-image]][OpenTelemetry-exporter-zipkin-nuget-url]           |
| Prometheus           | [![MyGet Nightly][OpenTelemetry-exporter-prom-myget-image]][OpenTelemetry-exporter-prom-myget-url]               | [![NuGet release][OpenTelemetry-exporter-prom-nuget-image]][OpenTelemetry-exporter-prom-nuget-url]               |
| Application Insights | [![MyGet Nightly][OpenTelemetry-exporter-ai-myget-image]][OpenTelemetry-exporter-ai-myget-url]                   | [![NuGet release][OpenTelemetry-exporter-ai-nuget-image]][OpenTelemetry-exporter-ai-nuget-url]                   |
| Stackdriver          | [![MyGet Nightly][OpenTelemetry-exporter-stackdriver-myget-image]][OpenTelemetry-exporter-stackdriver-myget-url] | [![NuGet release][OpenTelemetry-exporter-stackdriver-nuget-image]][OpenTelemetry-exporter-stackdriver-nuget-url] |
| Jaeger               | [![MyGet Nightly][OpenTelemetry-exporter-jaeger-myget-image]][OpenTelemetry-exporter-jaeger-myget-url]           | [![NuGet release][OpenTelemetry-exporter-jaeger-nuget-image]][OpenTelemetry-exporter-jaeger-nuget-url]           |
| LightStep            | [![MyGet Nightly][OpenTelemetry-exporter-lightstep-myget-image]][OpenTelemetry-exporter-lightstep-myget-url]     | [![NuGet release][OpenTelemetry-exporter-lightstep-nuget-image]][OpenTelemetry-exporter-lightstep-nuget-url]     |
| NewRelic             |                                                                                                                  | [![NuGet release][OpenTelemetry-exporter-newrelic-nuget-image]][OpenTelemetry-exporter-newrelic-nuget-url]       |
| Console              | [![MyGet Nightly][OpenTelemetry-exporter-console-myget-image]][OpenTelemetry-exporter-console-myget-url]         |                                                                                                                  |


## OpenTelemetry Tracing QuickStart: collecting data

You can use OpenTelemetry API to instrument code and report data.  Check out [Tracing API overview](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/overview.md) to learn more about distributed tracing.

In the examples below we demonstrate how to create and enrich spans though OpenTelemetry API.

OpenTelemetry also provides auto-collectors for ASP.NET Core, HttpClient calls (.NET Core) and Azure SDKs - [configuration](#configuration) section demonstrates how to enable it.

### Obtaining tracer

**Applications** should follow [configuration](#configuration) section to find out how to create/obtain tracer.

**Libraries** must take dependency on OpenTelemetry API package only and should never instantiate tracer or configure OpenTelemetry. Libraries **will** be able to obtain *global* tracer that may either be noop (if user application is not instrumented with OpenTelemetry) or real tracer implemented in the SDK package.

### Create basic span

To create the most basic span, you only specify the name. OpenTelemetry SDK collects start/end timestamps, assigns tracing context and assumes status of this span is `OK`.

```csharp
var span = tracer.StartSpan("basic span");
// ...
span.End();
```

### Create nested spans

In many cases you want to collect nested operations. You can propagate parent spans explicitly in your code or use implicit context propagation embedded into OpenTelemetry and .NET.

#### Explicit parent propagation and assignment

```csharp
var parentSpan = tracer.StartSpan("parent span");

// explicitly assigning parent here
var childSpan = tracer.StartSpan("child span", parentSpan);

childSpan.End();
parentSpan.End();
```

#### Implicit parent propagation and assignment

```csharp

// calling StartActiveSpan starts a span and puts parentSpan into the ambient context
// that flows in async calls.   When child is created, it implicitly becomes child of current span
using (tracer.StartActiveSpan("parent span", out _))
{
    var childSpan = tracer.StartSpan("child span");

    childSpan.End();
}

// parent span is ended when StartActiveSpan result is disposed
```

### Span with attributes

Attributes provide additional context on span specific to specific operation it tracks such as HTTP/DB/etc call properties.

```csharp
// spans have Client, Server, Internal, Producer and Consumer kinds to help visualize them
var span = tracer.StartSpan("span with attributes", SpanKind.Client);

// attributes specific to the call
span.SetAttribute("db.type", "redis");
span.SetAttribute("db.instance", "localhost:6379[0]");
span.SetAttribute("db.statement", "SET");
span.End();
```

### Span with links

[Links](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/overview.md#links-between-spans) allow to create relationships between different traces i.e. allow spans to have multiple relatives. They are typically used to tracer batching scenarios where multiple traces are merged into another one.

Links affect sampling decision and should be added before sampling decision is made (i.e. before span starts).

```csharp
SpanContext link1 = ExtractContext(eventHubMessage1);
SpanContext link2 = ExtractContext(eventHubMessage2);

var span = tracer.StartSpan("span with links", SpanKind.Server, DateTime.UtcNow, new [] {link1, link2});
span.End();
```

### Span with events

Events are timed text (with optional attributes) annotations on the span. Events can be added to current span (or any running span).

```csharp
using (tracer.StartActiveSpan("incoming HTTP request", SpanKind.Server, out var span))
{
    span.AddEvent("routes resolved");
}

// span is ended when StartActiveSpan result is disposed
```

### Context propagation out of process

When instrumenting transport-layer operations, instrumentation should support context propagation.

```csharp
// this extracts W3C trace-context from incoming HTTP request
// context may be valid if present and correct in the headers
// or invalid if there was no context (or it was not valid)
// instrumentation code should not care about it
var context = tracer.TextFormat.Extract(incomingRequest.Headers, (headers, name) => headers[name]);

var incomingSpan = tracer.StartSpan("incoming http request", context, SpanKind.Server);

var outgoingRequest = new HttpRequestMessage(HttpMethod.Get, "http://microsoft.com");
var outgoingSpan = tracer.StartSpan("outgoing http request", SpanKind.Client);

// now that we have outgoing span, we can inject it's context
// Note that if there is no SDK configured, tracer is noop -
// it creates noop spans with invalid context. we should not propagate it.
if (outgoingSpan.Context.IsValid)
{
    tracer.TextFormat.Inject(
        outgoingSpan.Context,
        outgoingRequest.Headers,
        (headers, name, value) => headers.Add(name, value));
}

// make outgoing call
// ...

outgoingSpan.End();
incomingSpan.End();
```

### Auto-collector implementation for Activity/DiagnosticSource

`System.Diagnostics.Activity` is similar to OpenTelemetry Span. HttpClient, ASP.NET Core, Azure SDKs use them to expose diagnostics events and context.

Leaving aside subscription mechanism, here is an example how you may implement callbacks for Start/Stop Activity

```csharp
void StartActivity()
{
    var span = tracer.StartSpanFromActivity("GET api/values", Activity.Current);

    // extract other things from Activity and set them on span (tags to attributes)
    // ...
}

void StopActivity()
{
    var span = tracer.CurrentSpan;
	
	span.End();
    if (span is IDisposable disposableSpan)
    {
        disposableSpan.Dispose();
    }
}
```

## Configuration

Configuration is done by user application: it should configure exporter and may also tune sampler and other properties.

### Basic Configuration

1. Install packages to your project:
   [OpenTelemetry][OpenTelemetry-nuget-url]
   [OpenTelemetry.Exporter.Zipkin][OpenTelemetry-exporter-zipkin-nuget-url]

2. Create `TracerFactory`

    ```csharp
    using (TracerFactory.Create(builder => builder
            .UseZipkin()
            .SetResource(Resources.CreateServiceResource("http-client-test")))
    {
        // ...
    }
    ```

### Configuration with Microsoft.Extensions.DependencyInjection

1. Install packages to your project:
   [OpenTelemetry.Hosting][OpenTelemetry-hosting-nuget-url] to provide `AddOpenTelemetry` helper method
   [OpenTelemetry.Collector.AspNetCore][OpenTelemetry-collect-aspnetcore-nuget-url] to collect incoming HTTP requests
   [OpenTelemetry.Collector.Dependencies](OpenTelemetry-collect-deps-nuget-url) to collect outgoing HTTP requests and Azure SDK calls

2. Make sure `TracerFactory`, is registered in DI.

    ```csharp
    services.AddOpenTelemetry(builder =>
    {
        builder
            .SetSampler(new AlwaysSampleSampler())
            .UseZipkin()

            // you may also configure request and dependencies collectors
            .AddRequestCollector()
            .AddDependencyCollector()
            .SetResource(Resources.CreateServiceResource("my-service"))
    });
    ```

### Using StackExchange.Redis collector

Outgoing http calls to Redis made using StackExchange.Redis library can be automatically tracked.

1. Install package to your project:
   [OpenTelemetry.Collector.StackExchangeRedis][OpenTelemetry-collect-stackexchange-redis-nuget-url]

2. Configure Redis collector

    ```csharp
    // connect to the server
    var connection = ConnectionMultiplexer.Connect("localhost:6379");

    using (TracerFactory.Create(b => b
                .SetSampler(new AlwaysSampleSampler())
                .UseZipkin()
                .SetResource(Resources.CreateServiceResource("my-service"))
                .AddCollector(t =>
                {
                    var collector = new StackExchangeRedisCallsCollector(t);
                    connection.RegisterProfiler(collector.GetProfilerSessionsFactory());
                    return collector;
                })))
    {

    }
    ```

You can combine it with dependency injection as shown in previous example.

### Custom samplers

You may configure sampler of your choice

```csharp
 using (TracerFactory.Create(b => b
            .SetSampler(new ProbabilitySampler(0.1))
            .UseZipkin()
            .SetResource(Resources.CreateServiceResource("my-service")))
{

}
```

You can also implement custom sampler by implementing `ISampler` interface

```csharp
class MySampler : Sampler
{
    public override string Description { get; } = "my custom sampler";

    public override Decision ShouldSample(SpanContext parentContext, ActivityTraceId traceId, ActivitySpanId spanId, string name,
        IDictionary<string, object> attributes, IEnumerable<Link> links)
    {
        bool sampledIn;
        if (parentContext != null && parentContext.IsValid)
        {
            sampledIn = (parentContext.TraceOptions & ActivityTraceFlags.Recorded) != 0;
        }
        else
        {
            sampledIn = Stopwatch.GetTimestamp() % 2 == 0;
        }

        return new Decision(sampledIn);
    }
}
```

## OpenTelemetry QuickStart: exporting data

### Using the Jaeger exporter

The Jaeger exporter communicates to a Jaeger Agent through the compact thrift protocol on
the Compact Thrift API port. You can configure the Jaeger exporter by following the directions below:

1. [Get Jaeger][jaeger-get-started].
2. Configure the `JaegerExporter`
    - `ServiceName`: The name of your application or service.
    - `AgentHost`: Usually `localhost` since an agent should usually be running on the same machine as your application or service.
    - `AgentPort`: The compact thrift protocol port of the Jaeger Agent (default `6831`)
    - `MaxPacketSize`: The maximum size of each UDP packet that gets sent to the agent. (default `65000`)
3. See the [sample][jaeger-sample] for an example of how to use the exporter.

```csharp
using (var tracerFactory = TracerFactory.Create(
    builder => builder.UseJaeger(o =>
    {
        o.ServiceName = "jaeger-test";
        o.AgentHost = "<jaeger server>";
    })))
{
    var tracer = tracerFactory.GetTracer("jaeger-test");
    using (tracer.StartActiveSpan("incoming request", out var span))
    {
        span.SetAttribute("custom-attribute", 55);
        await Task.Delay(1000);
    }
}
```

### Using Zipkin exporter

Configure Zipkin exporter to see traces in Zipkin UI.

1. Get Zipkin using [getting started guide][zipkin-get-started].
2. Configure `ZipkinTraceExporter` as below:
3. See [sample][zipkin-sample] for example use.

```csharp
using (var tracerFactory = TracerFactory.Create(
    builder => builder.UseZipkin(o =>
        o.ServiceName = "my-service";
        o.Endpoint = new Uri("https://<zipkin-server:9411>/api/v2/spans"))))
{
    var tracer = tracerFactory.GetTracer("zipkin-test");
    var span = tracer
        .SpanBuilder("incoming request")
        .StartSpan();

    await Task.Delay(1000);
    span.End();
}
```

### Using Prometheus exporter

Configure Prometheus exporter to have stats collected by Prometheus.

1. Get Prometheus using [getting started guide][prometheus-get-started].
2. Start `PrometheusExporter` as below.
3. See [sample][prometheus-sample] for example use.

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
### Using the NewRelic exporter

The New Relic OpenTelemetry Trace Exporter is a OpenTelemetry Provider that sends data from .NET applications to New Relic.
It uses the NewRelic SDK to send Traces to the New Relic backend

Please refer to the New Relic Exporter [Documentation][newrelic-get-started]

### Using Stackdriver Exporter

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
using (var tracerFactory = TracerFactory.Create(builder => builder
    .AddProcessorPipeline(c => c.SetExporter(new StackdriverTraceExporter("YOUR-GOOGLE-PROJECT-ID")))))
{
    var tracer = tracerFactory.GetTracer("stackdriver-test");
    var span = tracer
        .SpanBuilder("incoming request")
        .StartSpan();

    await Task.Delay(1000);
    span.End();
}
```

#### Metrics

```csharp
var metricExporter = new StackdriverExporter(
    "YOUR-GOOGLE-PROJECT-ID",
    Stats.ViewManager);
metricExporter.Start();
```

### Using Application Insights exporter

1. Create [Application Insights][ai-get-started] resource.
2. Set instrumentation key via telemetry configuration object
   (`new TelemetryConfiguration("iKey")`). This object may be injected via
   dependency injection as well.
3. Instantiate a new instance of `ApplicationInsightsExporter`.
4. See [sample][ai-sample] for example use.

``` csharp
using (var tracerFactory = TracerFactory.Create(builder => builder
    .UseApplicationInsights(config => config.InstrumentationKey = "instrumentation-key")))
{
    var tracer = tracerFactory.GetTracer("application-insights-test");
    var span = tracer
        .SpanBuilder("incoming request")
        .StartSpan();

    await Task.Delay(1000);
    span.End();
}
```

### Using LightStep exporter

Configure LightStep exporter to see traces in [LightStep](https://lightstep.com/).

1. Setup LightStep using [getting started](lightstep-getting-started) guide
2. Configure `LightStepTraceExporter` (see below)
3. See [sample](lightstep-sample) for example use

```csharp
using (var tracerFactory = TracerFactory.Create(
    builder => builder.UseLightStep(o =>
        {
            o.AccessToken = "<access-token>";
            o.ServiceName = "lightstep-test";
        })))
{
    var tracer = tracerFactory.GetTracer("lightstep-test");
    using (tracer.StartActiveSpan("incoming request", out var span))
    {
        span.SetAttribute("custom-attribute", 55);
        await Task.Delay(1000);
    }
}
```

### Implementing your own exporter

#### Tracing

Exporters should subclass `SpanExporter` and implement `ExportAsync` and `Shutdown` methods.
Depending on user's choice and load on the application `ExportAsync` may get called concurrently with zero or more spans.
Exporters should expect to receive only sampled-in ended spans. Exporters must not throw. Exporters should not modify spans they receive (the same span may be exported again by different exporter).

It's a good practice to make exporter `IDisposable` and shut it down in IDispose unless it was shut down explicitly. This helps when exporters are registered with dependency injection framework and their lifetime is tight to the app lifetime.

```csharp
class MyExporter : SpanExporter
{
    public override Task<ExportResult> ExportAsync(IEnumerable<Span> batch, CancellationToken cancellationToken)
    {
        foreach (var span in batch)
        {
            Console.WriteLine($"[{span.StartTimestamp:o}] {span.Name} {span.Context.TraceId.ToHexString()} {span.Context.SpanId.ToHexString()}");
        }

        return Task.FromResult(ExportResult.Success);
    }

    public override Task ShutdownAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
```

Users may configure the exporter similarly to other exporters.
You should also provide additional methods to simplify configuration similarly to `UseZipkin` extension method.

```csharp
var exporter = new MyExporter();
using (var tracerFactory = TracerFactory.Create(
    builder => builder.AddProcessorPipeline(b => b.SetExporter(new MyExporter())))
{
    // ...
}
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
OpenTelemetry libraries, then there is no deprecation period. Otherwise, we will
deprecate it for 18 months before removing it, if possible.

[main-gitter-image]: https://badges.gitter.im/open-telemetry/community.svg
[main-gitter-url]:https://gitter.im/open-telemetry/community?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge
[dotnet-gitter-image]: https://badges.gitter.im/open-telemetry/opentelemetry-dotnet.svg
[dotnet-gitter-url]:https://gitter.im/open-telemetry/opentelemetry-dotnet?utm_source=badge&utm_medium=badge&utm_campaign=pr-badge&utm_content=badge
[OpenTelemetry-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.svg
[OpenTelemetry-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry
[OpenTelemetry-abs-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Api.svg
[OpenTelemetry-abs-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Api
[OpenTelemetry-exporter-zipkin-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.Zipkin.svg
[OpenTelemetry-exporter-zipkin-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.Zipkin
[OpenTelemetry-exporter-jaeger-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.Jaeger.svg
[OpenTelemetry-exporter-jaeger-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.Jaeger
[OpenTelemetry-exporter-prom-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.Prometheus.svg
[OpenTelemetry-exporter-prom-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.Prometheus
[OpenTelemetry-exporter-ai-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.ApplicationInsights.svg
[OpenTelemetry-exporter-ai-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.ApplicationInsights
[OpenTelemetry-exporter-stackdriver-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.Stackdriver.svg
[OpenTelemetry-exporter-stackdriver-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.Stackdriver
[OpenTelemetry-exporter-lightstep-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.LightStep.svg
[OpenTelemetry-exporter-lightstep-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.LightStep
[OpenTelemetry-exporter-console-myget-image]: https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.Console.svg
[OpenTelemetry-exporter-console-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.Console
[OpenTelemetry-collect-aspnetcore-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Collector.AspNetCore.svg
[OpenTelemetry-collect-aspnetcore-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Collector.AspNetCore
[OpenTelemetry-collect-deps-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Collector.Dependencies.svg
[OpenTelemetry-collect-deps-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Collector.Dependencies
[OpenTelemetry-collect-stackexchange-redis-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Collector.StackExchangeRedis.svg
[OpenTelemetry-collect-stackexchange-redis-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Collector.StackExchangeRedis
[OpenTelemetry-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.svg
[OpenTelemetry-nuget-url]:https://www.nuget.org/packages/OpenTelemetry
[OpenTelemetry-hosting-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Hosting.svg
[OpenTelemetry-hosting-nuget-url]:https://www.nuget.org/packages/OpenTelemetry.Hosting
[OpenTelemetry-abs-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Api.svg
[OpenTelemetry-abs-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Api
[OpenTelemetry-exporter-zipkin-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.Zipkin.svg
[OpenTelemetry-exporter-zipkin-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Zipkin
[OpenTelemetry-exporter-jaeger-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.Jaeger.svg
[OpenTelemetry-exporter-jaeger-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Jaeger
[OpenTelemetry-exporter-prom-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.Prometheus.svg
[OpenTelemetry-exporter-prom-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus
[OpenTelemetry-exporter-ai-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.ApplicationInsights.svg
[OpenTelemetry-exporter-ai-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.ApplicationInsights
[OpenTelemetry-exporter-stackdriver-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.Stackdriver.svg
[OpenTelemetry-exporter-stackdriver-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Stackdriver
[OpenTelemetry-exporter-lightstep-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.LightStep.svg
[OpenTelemetry-exporter-lightstep-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Lightstep
[OpenTelemetry-exporter-newrelic-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.NewRelic.svg
[OpenTelemetry-exporter-newrelic-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.NewRelic
[OpenTelemetry-collect-aspnetcore-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Collector.AspNetCore.svg
[OpenTelemetry-collect-aspnetcore-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Collector.AspNetCore
[OpenTelemetry-collect-deps-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Collector.Dependencies.svg
[OpenTelemetry-collect-deps-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Collector.Dependencies
[OpenTelemetry-collect-stackexchange-redis-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Collector.StackExchangeRedis.svg
[OpenTelemetry-collect-stackexchange-redis-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Collector.StackExchangeRedis
[up-for-grabs-issues]: https://github.com/open-telemetry/OpenTelemetry-dotnet/issues?q=is%3Aissue+is%3Aopen+label%3Aup-for-grabs
[good-first-issues]: https://github.com/open-telemetry/OpenTelemetry-dotnet/issues?q=is%3Aissue+is%3Aopen+label%3A%22good+first+issue%22
[zipkin-get-started]: https://zipkin.io/pages/quickstart.html
[jaeger-get-started]: https://www.jaegertracing.io/docs/1.13/getting-started/
[newrelic-get-started]: https://github.com/newrelic/newrelic-telemetry-sdk-dotnet/blob/master/src/OpenTelemetry.Exporter.NewRelic/README.md
[ai-get-started]: https://docs.microsoft.com/azure/application-insights
[stackdriver-trace-setup]: https://cloud.google.com/trace/docs/setup/
[stackdriver-monitoring-setup]: https://cloud.google.com/monitoring/api/enable-api
[GAE]: https://cloud.google.com/appengine/docs/flexible/dotnet/quickstart
[GKE]: https://codelabs.developers.google.com/codelabs/cloud-kubernetes-aspnetcore/index.html?index=..%2F..index#0
[gcp-auth]: https://cloud.google.com/docs/authentication/getting-started
[semver]: http://semver.org/
[ai-sample]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/TestApplicationInsights.cs
[stackdriver-sample]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/TestStackdriver.cs
[zipkin-sample]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/TestZipkin.cs
[jaeger-sample]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/TestJaeger.cs
[prometheus-get-started]: https://prometheus.io/docs/introduction/first_steps/
[prometheus-sample]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/TestPrometheus.cs
[lightstep-getting-started]: https://docs.lightstep.com/docs/welcome-to-lightstep
[lightstep-sample]: https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/samples/Exporters/TestLightstep.cs

