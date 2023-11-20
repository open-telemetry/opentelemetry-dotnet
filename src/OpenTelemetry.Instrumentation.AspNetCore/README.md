# ASP.NET Core Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AspNetCore.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.AspNetCore.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [ASP.NET Core](https://docs.microsoft.com/aspnet/core) and
collect metrics and traces about incoming web requests. This instrumentation
also collects traces from incoming gRPC requests using
[Grpc.AspNetCore](https://www.nuget.org/packages/Grpc.AspNetCore).

**Note: This component is based on the OpenTelemetry semantic conventions for
[metrics](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/http/http-metrics.md)
and
[traces](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/http/http-spans.md).
These conventions are
[Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md),
and hence, this package is a [pre-release](../../VERSIONING.md#pre-releases).
Until a [stable
version](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/telemetry-stability.md)
is released, there can be breaking changes. You can track the progress from
[milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestone/23).**

## Steps to enable OpenTelemetry.Instrumentation.AspNetCore

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.AspNetCore`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package --prerelease OpenTelemetry.Instrumentation.AspNetCore
```

### Step 2: Enable ASP.NET Core Instrumentation at application startup

ASP.NET Core instrumentation must be enabled at application startup. This is
typically done in the `ConfigureServices` of your `Startup` class. Both examples
below enables OpenTelemetry by calling `AddOpenTelemetry()` on `IServiceCollection`.
 This extension method requires adding the package
[`OpenTelemetry.Extensions.Hosting`](../OpenTelemetry.Extensions.Hosting/README.md)
to the application. This ensures instrumentations are disposed when the host
is shutdown.

#### Traces

The following example demonstrates adding ASP.NET Core instrumentation with the
extension method `WithTracing()` on `OpenTelemetryBuilder`.
then extension method `AddAspNetCoreInstrumentation()` on `TracerProviderBuilder`
to the application. This example also sets up the Console Exporter,
which requires adding the package [`OpenTelemetry.Exporter.Console`](../OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

public void ConfigureServices(IServiceCollection services)
{
    services.AddOpenTelemetry()
        .WithTracing(builder => builder
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter());
}
```

#### Metrics

The following example demonstrates adding ASP.NET Core instrumentation with the
extension method `WithMetrics()` on `OpenTelemetryBuilder`
then extension method `AddAspNetCoreInstrumentation()` on `MeterProviderBuilder`
to the application. This example also sets up the Console Exporter,
which requires adding the package [`OpenTelemetry.Exporter.Console`](../OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;

public void ConfigureServices(IServiceCollection services)
{
    services.AddOpenTelemetry()
        .WithMetrics(builder => builder
            .AddAspNetCoreInstrumentation()
            .AddConsoleExporter());
}
```

#### List of metrics produced

A different metric is emitted depending on whether a user opts-in to the new
Http Semantic Conventions using `OTEL_SEMCONV_STABILITY_OPT_IN`.

* By default, the instrumentation emits the following metric.

    | Name  | Instrument Type | Unit | Description | Attributes |
    |-------|-----------------|------|-------------|------------|
    | `http.server.duration` | Histogram | `ms` | Measures the duration of inbound HTTP requests. | http.flavor, http.scheme, http.method, http.status_code, net.host.name, net.host.port, http.route |

* If user sets the environment variable to `http`, the instrumentation emits
  the following metric.

    | Name  | Instrument Type | Unit | Description | Attributes |
    |-------|-----------------|------|-------------|------------|
    | `http.server.request.duration` | Histogram | `s` | Measures the duration of inbound HTTP requests. | network.protocol.version, url.scheme, http.request.method, http.response.status_code, http.route |

    This metric is emitted in `seconds` as per the semantic convention. While
    the convention [recommends using custom histogram buckets](https://github.com/open-telemetry/semantic-conventions/blob/2bad9afad58fbd6b33cc683d1ad1f006e35e4a5d/docs/http/http-metrics.md)
    , this feature is not yet available via .NET Metrics API.
    A [workaround](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4820)
    has been included in OTel SDK starting version `1.6.0` which applies
    recommended buckets by default for `http.server.request.duration`.

* If user sets the environment variable to `http/dup`, the instrumentation
  emits both `http.server.duration` and `http.server.request.duration`.

##### Metrics on ASP.NET Core 8.0 and above frameworks

This library enables all [built-in
metrics](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics-aspnetcore)
by default when targeting in ASP.NET Core 8.0 and above framework.
[Views](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics/customizing-the-sdk#drop-an-instrument)
can be used to opt-out of metrics that you do not need. Alternatively, you can
also enable selected set of metrics by calling `AddMeter()` extension on
`MeterProviderBuilder` for meters listed in
[built-in-metrics-aspnetcore](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics-aspnetcore).
Note that if you choose to enable metrics via `AddMeter()` then you do not need
to call `AddAspNetCoreInstrumentation()` on `MeterProviderBuilder`.

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`AspNetCoreInstrumentationOptions`, which allows adding [`Filter`](#filter),
[`Enrich`](#enrich) as explained below.

// TODO: This section could be refined.
When used with [`OpenTelemetry.Extensions.Hosting`](../OpenTelemetry.Extensions.Hosting/README.md),
all configurations to `AspNetCoreInstrumentationOptions` can be done in the `ConfigureServices`
method of you applications `Startup` class as shown below.

```csharp
// Configure
services.Configure<AspNetCoreInstrumentationOptions>(options =>
{
    options.Filter = (httpContext) =>
    {
        // only collect telemetry about HTTP GET requests
        return httpContext.Request.Method.Equals("GET");
    };
});

services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation()
        .AddConsoleExporter());
```

### Filter

This instrumentation by default collects all the incoming http requests. It
allows filtering of requests by using the `Filter` function in
`AspNetCoreInstrumentationOptions`. This defines the condition for allowable
requests. The Filter receives the `HttpContext` of the incoming
request, and does not collect telemetry about the request if the Filter
returns false or throws exception.

The following code snippet shows how to use `Filter` to only allow GET
requests.

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation((options) => options.Filter = httpContext =>
        {
            // only collect telemetry about HTTP GET requests
            return httpContext.Request.Method.Equals("GET");
        })
        .AddConsoleExporter());
```

It is important to note that this `Filter` option is specific to this
instrumentation. OpenTelemetry has a concept of a
[Sampler](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampling),
and the `Filter` option does the filtering *after* the Sampler is invoked.

### Enrich

This instrumentation library provides `EnrichWithHttpRequest`,
`EnrichWithHttpResponse` and `EnrichWithException` options that can be used to
enrich the activity with additional information from the raw `HttpRequest`,
`HttpResponse` and `Exception` objects respectively. These actions are called
only when `activity.IsAllDataRequested` is `true`. It contains the activity
itself (which can be enriched) and the actual raw object.

The following code snippet shows how to enrich the activity using all 3
different options.

```csharp
services.AddOpenTelemetry()
    .WithTracing(builder => builder
        .AddAspNetCoreInstrumentation(o =>
        {
            o.EnrichWithHttpRequest = (activity, httpRequest) =>
            {
                activity.SetTag("requestProtocol", httpRequest.Protocol);
            };
            o.EnrichWithHttpResponse = (activity, httpResponse) =>
            {
                activity.SetTag("responseLength", httpResponse.ContentLength);
            };
            o.EnrichWithException = (activity, exception) =>
            {
                activity.SetTag("exceptionType", exception.GetType().ToString());
            };
        }));
```

[Processor](../../docs/trace/extending-the-sdk/README.md#processor),
is the general extensibility point to add additional properties to any activity.
The `Enrich` option is specific to this instrumentation, and is provided to
get access to `HttpRequest` and `HttpResponse`.

### RecordException

This instrumentation automatically sets Activity Status to Error if an unhandled
exception is thrown. Additionally, `RecordException` feature may be turned on,
to store the exception to the Activity itself as ActivityEvent.

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Instrumentation-AspNetCore" for its internal
logging. Please refer to [SDK
troubleshooting](../OpenTelemetry/README.md#troubleshooting) for instructions on
seeing these internal logs.

## References

* [Introduction to ASP.NET
  Core](https://docs.microsoft.com/aspnet/core/introduction-to-aspnet-core)
* [gRPC services using ASP.NET Core](https://docs.microsoft.com/aspnet/core/grpc/aspnetcore)
* [OpenTelemetry Project](https://opentelemetry.io/)
