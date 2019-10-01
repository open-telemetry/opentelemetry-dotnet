# OpenTelemetry .NET SDK - distributed tracing and stats collection framework

.NET Channel: [![Gitter chat][dotnet-gitter-image]][dotnet-gitter-url]

Community Channel: [![Gitter chat][main-gitter-image]][main-gitter-url]

We hold regular meetings. See details at [community page](https://github.com/open-telemetry/community#net-sdk).

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
| OpenTelemetry.Abstractions | [![MyGet Nightly][OpenTelemetry-abs-myget-image]][OpenTelemetry-abs-myget-url] | [![NuGet Release][OpenTelemetry-abs-nuget-image]][OpenTelemetry-abs-nuget-url] |

### Data Collectors

| Package                           | MyGet (CI)                                                                                                                     | NuGet (releases)                                                                                                               |
| --------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ | ------------------------------------------------------------------------------------------------------------------------------ |
| Asp.Net Core                      | [![MyGet Nightly][OpenTelemetry-collect-aspnetcore-myget-image]][OpenTelemetry-collect-aspnetcore-myget-url]                   | [![NuGet Release][OpenTelemetry-collect-aspnetcore-nuget-image]][OpenTelemetry-collect-aspnetcore-nuget-url]                   |
| .Net Core HttpClient & Azure SDKs | [![MyGet Nightly][OpenTelemetry-collect-deps-myget-image]][OpenTelemetry-collect-deps-myget-url]                               | [![NuGet Release][OpenTelemetry-collect-deps-nuget-image]][OpenTelemetry-collect-deps-nuget-url]                               |
| StackExchange.Redis               | [![MyGet Nightly][OpenTelemetry-collect-stackexchange-redis-myget-image]][OpenTelemetry-collect-stackexchange-redis-myget-url] | [![NuGet Release][OpenTelemetry-collect-stackexchange-redis-nuget-image]][OpenTelemetry-collect-stackexchange-redis-nuget-url] |

### Exporters Packages

| Package              | MyGet (CI)                                                                                                       | NuGet (releases)                                                                                                 |
| -------------------- | ---------------------------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------- |
| Zipkin               | [![MyGet Nightly][OpenTelemetry-exporter-zipkin-myget-image]][OpenTelemetry-exporter-zipkin-myget-url]           | [![NuGet release][OpenTelemetry-exporter-zipkin-nuget-image]][OpenTelemetry-exporter-zipkin-nuget-url]           |
| Prometheus           | [![MyGet Nightly][OpenTelemetry-exporter-prom-myget-image]][OpenTelemetry-exporter-prom-myget-url]               | [![NuGet release][OpenTelemetry-exporter-prom-nuget-image]][OpenTelemetry-exporter-prom-nuget-url]               |
| Application Insights | [![MyGet Nightly][OpenTelemetry-exporter-ai-myget-image]][OpenTelemetry-exporter-ai-myget-url]                   | [![NuGet release][OpenTelemetry-exporter-ai-nuget-image]][OpenTelemetry-exporter-ai-nuget-url]                   |
| Stackdriver          | [![MyGet Nightly][OpenTelemetry-exporter-stackdriver-myget-image]][OpenTelemetry-exporter-stackdriver-myget-url] | [![NuGet release][OpenTelemetry-exporter-stackdriver-nuget-image]][OpenTelemetry-exporter-stackdriver-nuget-url] |

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
var span = tracer
    .SpanBuilder("basic span")
    .StartSpan();

span.End();
```

### Create nested spans

In many cases you want to collect nested operations. You can propagate parent spans explicitly in your code or use implicit context propagation embedded into OpenTelemetry and .NET.

#### Explicit parent propagation and assignment

```csharp
var parentSpan = tracer
    .SpanBuilder("parent")
    .StartSpan();

var childSpan = tracer
    .SpanBuilder("child")
    .SetParent(parentSpan) // explicitly assigning parent here
    .StartSpan();

childSpan.End();
parentSpan.End();
```

#### Implicit parent propagation and assignment

```csharp
var parentSpan = tracer
    .SpanBuilder("parent")
    .StartSpan();

// calling WithSpan puts parentSpan into the ambient context
// that flows in async calls.   When child is created, it
// implicitly becomes child of current span
using (tracer.WithSpan(parentSpan))
{
    var childSpan = tracer
        .SpanBuilder("child")
        .StartSpan();

    childSpan.End();
}

// parent span is ended when WithSpan result is disposed
```

### Span with attributes

Attributes provide additional context on span specific to specific operation it tracks such as HTTP/DB/etc call properties.

```csharp
var span = tracer
    .SpanBuilder("span with attributes")
    // spans have Client, Server, Internal, Producer and Consumer kinds to help visualize them
    .SetSpanKind(SpanKind.Client)
    .StartSpan();

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

var span = tracer
    .SpanBuilder("span with links")
    .SetSpanKind(SpanKind.Server)
    .AddLink(link1)
    .AddLink(link2)
    .StartSpan();

span.End();
```

### Span with events

Events are timed text (with optional attributes) annotations on the span. Events can be added to current span (or any running span).

```csharp
var span = tracer
    .SpanBuilder("incoming HTTP request")
    .SetSpanKind(SpanKind.Server)
    .StartSpan();

using (tracer.WithSpan(span))
{
    tracer.CurrentSpan.AddEvent("routes reolved");
}

// span is ended when WithSpan result is disposed
```

### Context propagation out of process

When instrumenting transport-layer operations, instrumentation should support context propagation.

```csharp
// this extracts W3C trace-context from incoming HTTP request
// context may be valid if present and correct in the headers
// or invalid if there was no context (or it was not valid)
// instrumentation code should not care about it
var context = tracer.TextFormat.Extract(incomingRequest.Headers, (headers, name) => headers[name]);

var incomingSpan = tracer
    .SpanBuilder("incoming http request")
    .SetSpanKind(SpanKind.Server)
    .SetParent(context)
    .StartSpan();

var outgoingRequest = new HttpRequestMessage(HttpMethod.Get, "http://microsoft.com");
var outgoingSpan = tracer
    .SpanBuilder("outgoing http request")
    .SetSpanKind(SpanKind.Client)
    .StartSpan();

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
    var span = tracer
        .SpanBuilder("GET api/values") // get name from Activity props/tags, DiagnosticSource payload
        .SetCreateChild(false) // instructs builder to use current activity without creating a child one for span
        .StartSpan();

    // extract other things from Activity and set them on span (tags to attributes)
    // ...

    tracer.WithSpan(span); // we drop scope here as we cannot propagate it
}

void StopActivity()
{
    var span = tracer.CurrentSpan;
    span.End();
}
```

## Configuration

Configuration is done by user application: it should configure exporter and may also tune sampler and other properties.

### Basic Configuration

1. Install packages to your project:
   [OpenTelemetry][OpenTelemetry-nuget-url]
   [OpenTelemetry.Exporter.Zipkin][OpenTelemetry-exporter-zipkin-nuget-url]

2. Create exporter and tracer

    ```csharp
    var zipkinExporter = new ZipkinTraceExporter(new ZipkinTraceExporterOptions());
    var tracer = new Tracer(new BatchingSpanProcessor(zipkinExporter), TraceConfig.Default);
    ```

### Configuration with Microsoft.Extensions.DependencyInjection

1. Install packages to your project:
   [OpenTelemetry][OpenTelemetry-nuget-url]
   [OpenTelemetry.Collector.AspNetCore][OpenTelemetry-collect-aspnetcore-nuget-url] to collect incoming HTTP requests
   [OpenTelemetry.Collector.Dependencies](OpenTelemetry-collect-deps-nuget-url) to collect outgoing HTTP requests and Azure SDK calls

2. Make sure `ITracer`, `ISampler`, and `SpanExporter` and `SpanProcessor` are registered in DI.

    ```csharp
    services.AddSingleton<ISampler>(Samplers.AlwaysSample);
    services.AddSingleton<ZipkinTraceExporterOptions>(_ => new ZipkinTraceExporterOptions { ServiceName = "my-service" });
    services.AddSingleton<SpanExporter, ZipkinTraceExporter>();
    services.AddSingleton<SpanProcessor, BatchingSpanProcessor>();
    services.AddSingleton<TraceConfig>();
    services.AddSingleton<ITracer, Tracer>();

    // you may also configure request and dependencies collectors
    services.AddSingleton<RequestsCollectorOptions>(new RequestsCollectorOptions());
    services.AddSingleton<RequestsCollector>();

    services.AddSingleton<DependenciesCollectorOptions>(new DependenciesCollectorOptions());
    services.AddSingleton<DependenciesCollector>();
    ```

3. Start auto-collectors

   To start collection, `RequestsCollector` and `DependenciesCollector` need to be resolved.

    ```csharp
    public void Configure(IApplicationBuilder app, RequestsCollector requestsCollector,  DependenciesCollector dependenciesCollector)
    {
        // ...
    }
    ```

### Using StackExchange.Redis collector

Outgoing http calls to Redis made using StackExchange.Redis library can be automatically tracked.

1. Install package to your project:
   [OpenTelemetry.Collector.StackExchangeRedis][OpenTelemetry-collect-stackexchange-redis-nuget-url]

2. Make sure `ITracer`, `ISampler`, and `SpanExporter` and `SpanProcessor` are registered in DI.

    ```csharp
    services.AddSingleton<ISampler>(Samplers.AlwaysSample);
    services.AddSingleton<ZipkinTraceExporterOptions>(_ => new ZipkinTraceExporterOptions { ServiceName = "my-service" });
    services.AddSingleton<SpanExporter, ZipkinTraceExporter>();
    services.AddSingleton<SpanProcessor, BatchingSpanProcessor>();
    services.AddSingleton<TraceConfig>();
    services.AddSingleton<ITracer, Tracer>();

    // configure redis collection
    services.AddSingleton<StackExchangeRedisCallsCollectorOptions>(new StackExchangeRedisCallsCollectorOptions());
    services.AddSingleton<StackExchangeRedisCallsCollector>();
    ```

3. Start auto-collectors

    To start collection, `StackExchangeRedisCallsCollector` needs to be resolved.

    ```csharp
    public void Configure(IApplicationBuilder app, StackExchangeRedisCallsCollector redisCollector)
    {
        // ...
    }

### Custom samplers

You may configure sampler of your choice

```csharp
var sampler = ProbabilitySampler.Create(0.1);
var zipkinExporter = new ZipkinTraceExporter(new ZipkinTraceExporterOptions())
var tracer = new Tracer(new SimpleSpanProcessor(zipkinExporter), new TraceConfig(sampler));
```

You can also implement custom sampler by implementing `ISampler` interface

```csharp
class MySampler : ISampler
{
    public string Description { get; } = "my custom sampler";

    public Decision ShouldSample(SpanContext parentContext, ActivityTraceId traceId, ActivitySpanId spanId, string name,
        IEnumerable<ILink> links)
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
    - `AgengHost`: Usually `localhost` since an agent should 
    usually be running on the same machine as your application or service.
    - `AgentPort`: The compact thrift protocol port of the Jaeger Agent (default `6831`)
    - `MaxPacketSize`: The maximum size of each UDP packet that gets sent to the agent. (default `65000`)
3. See the [sample][jaeger-sample] for an example of how to use the exporter.

```csharp
var jaegerOptions = new JaegerExporterOptions()
        {
            ServiceName = "tracing-to-jaeger-service",
            AgentHost = host,
            AgentPort = port,
        };

var exporter = new JaegerTraceExporter(jaegerOptions);

var tracer = new Tracer(new BatchingSpanProcessor(exporter), TraceConfig.Default);

var span = tracer
            .SpanBuilder("incoming request")
            .SetSampler(Samplers.AlwaysSample)
            .StartSpan();

await Task.Delay(1000);
span.End();

// Gracefully shutdown the exporter so it'll flush queued traces to Jaeger.
// you may need to catch `OperationCancelledException` here
await exporter.ShutdownAsync(CancellationToken.None);
```

### Using Zipkin exporter

Configure Zipkin exporter to see traces in Zipkin UI.

1. Get Zipkin using [getting started guide][zipkin-get-started].
2. Start `ZipkinTraceExporter` as below:
3. See [sample][zipkin-sample] for example use.

```csharp
var exporter = new ZipkinTraceExporter(
  new ZipkinTraceExporterOptions() {
    Endpoint = new Uri("https://<zipkin-server:9411>/api/v2/spans"),
    ServiceName = typeof(Program).Assembly.GetName().Name,
  });
var tracer = new Tracer(new BatchingSpanProcessor(exporter), TraceConfig.Default);

var span = tracer
            .SpanBuilder("incoming request")
            .SetSampler(Samplers.AlwaysSample)
            .StartSpan();

await Task.Delay(1000);
span.End();

// Gracefully shutdown the exporter so it'll flush queued traces to Jaeger.
// you may need to catch `OperationCancelledException` here
await exporter.ShutdownAsync(CancellationToken.None);
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

#### Traces

```csharp
var traceExporter = new StackdriverTraceExporter("YOUR-GOOGLE-PROJECT-ID");
var tracer = new Tracer(new BatchingSpanProcessor(exporter), TraceConfig.Default);

var span = tracer
            .SpanBuilder("incoming request")
            .SetSampler(Samplers.AlwaysSample)
            .StartSpan();

await Task.Delay(1000);
span.End();

// Gracefully shutdown the exporter so it'll flush queued traces to Jaeger.
// you may need to catch `OperationCancelledException` here
await exporter.ShutdownAsync(CancellationToken.None);
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
var config = new TelemetryConfiguration("iKey")
var exporter = new ApplicationInsightsExporter(config);
var tracer = new Tracer(new BatchingSpanProcessor(exporter), TraceConfig.Default);

var span = tracer
            .SpanBuilder("incoming request")
            .SetSampler(Samplers.AlwaysSample)
            .StartSpan();

await Task.Delay(1000);
span.End();

// Gracefully shutdown the exporter so it'll flush queued traces to Jaeger.
// you may need to catch `OperationCancelledException` here
await exporter.ShutdownAsync(CancellationToken.None);
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

Users may configure the exporter similarly to other exporters. You cay also provide additional methods to simplify it.

```csharp
var exporter = new MyExporter();
var tracer = new Tracer(new BatchingSpanProcessor(exporter), TraceConfig.Default);
await exporter.ShutdownAsync(CancellationToken.None);
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
[OpenTelemetry-abs-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Abstractions.svg
[OpenTelemetry-abs-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Abstractions
[OpenTelemetry-exporter-zipkin-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.Zipkin.svg
[OpenTelemetry-exporter-zipkin-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.Zipkin
[OpenTelemetry-exporter-prom-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.Prometheus.svg
[OpenTelemetry-exporter-prom-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.Prometheus
[OpenTelemetry-exporter-ai-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.ApplicationInsights.svg
[OpenTelemetry-exporter-ai-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.ApplicationInsights
[OpenTelemetry-exporter-stackdriver-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Exporter.Stackdriver.svg
[OpenTelemetry-exporter-stackdriver-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Exporter.Stackdriver
[OpenTelemetry-collect-aspnetcore-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Collector.AspNetCore.svg
[OpenTelemetry-collect-aspnetcore-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Collector.AspNetCore
[OpenTelemetry-collect-deps-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Collector.Dependencies.svg
[OpenTelemetry-collect-deps-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Collector.Dependencies
[OpenTelemetry-collect-stackexchange-redis-myget-image]:https://img.shields.io/myget/opentelemetry/vpre/OpenTelemetry.Collector.StackExchangeRedis.svg
[OpenTelemetry-collect-stackexchange-redis-myget-url]: https://www.myget.org/feed/opentelemetry/package/nuget/OpenTelemetry.Collector.StackExchangeRedis
[OpenTelemetry-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.svg
[OpenTelemetry-nuget-url]:https://www.nuget.org/packages/OpenTelemetry
[OpenTelemetry-abs-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Abstractions.svg
[OpenTelemetry-abs-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Abstractions
[OpenTelemetry-exporter-zipkin-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.Zipkin.svg
[OpenTelemetry-exporter-zipkin-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Zipkin
[OpenTelemetry-exporter-prom-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.Prometheus.svg
[OpenTelemetry-exporter-prom-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Prometheus
[OpenTelemetry-exporter-ai-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.ApplicationInsights.svg
[OpenTelemetry-exporter-ai-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.ApplicationInsights
[OpenTelemetry-exporter-stackdriver-nuget-image]:https://img.shields.io/nuget/vpre/OpenTelemetry.Exporter.Stackdriver.svg
[OpenTelemetry-exporter-stackdriver-nuget-url]: https://www.nuget.org/packages/OpenTelemetry.Exporter.Stackdriver
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

