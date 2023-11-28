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

When the application targets `.NET6.0` or `.NET7.0`, the instrumentation emits
the following metric:

| Name                              | Details                                                                                                                                                 |
|-----------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------|
| `http.server.request.duration`    | [Specification](https://github.com/open-telemetry/semantic-conventions/blob/release/v1.23.x/docs/http/http-metrics.md#metric-httpserverrequestduration) |

Starting from `.NET8.0`, metrics instrumentation is natively implemented, and
the ASP.NET Core library has incorporated support for [built-in
metrics](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics-aspnetcore)
following the OpenTelemetry semantic conventions. The library includes additional
metrics beyond those defined in the
[specification](https://github.com/open-telemetry/semantic-conventions/blob/v1.23.0/docs/http/http-metrics.md),
covering additional scenarios for ASP.NET Core users. When the application
targets `.NET8.0` and newer versions, the instrumentation library automatically
enables all `built-in` metrics by default.

Note that the `AddAspNetCoreInstrumentation()` extension simplifies the process
of enabling all built-in metrics via a single line of code. Alternatively, for
more granular control over emitted metrics, you can utilize the `AddMeter()`
extension on `MeterProviderBuilder` for meters listed in
[built-in-metrics-aspnetcore](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics-aspnetcore).
Using `AddMeter()` for metrics activation eliminates the need to take dependency
on the instrumentation library package and calling
`AddAspNetCoreInstrumentation()`.

If you utilize `AddAspNetCoreInstrumentation()` and wish to exclude unnecessary
metrics, you can utilize
[Views](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics/customizing-the-sdk#drop-an-instrument)
to achieve this.

**Note:** There is no difference in features or emitted metrics when enabling
metrics using `AddMeter()` or `AddAspNetCoreInstrumentation()` on `.NET8.0` and
newer versions.

> **Note**
> The `http.server.request.duration` metric is emitted in `seconds` as
    per the semantic convention. While the convention [recommends using custom
    histogram
    buckets](https://github.com/open-telemetry/semantic-conventions/blob/release/v1.23.x/docs/http/http-metrics.md)
    , this feature is not yet available via .NET Metrics API. A
    [workaround](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4820)
    has been included in OTel SDK starting version `1.6.0` which applies
    recommended buckets by default for `http.server.request.duration`. This
    applies to all targeted frameworks.

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

## Activity Duration and http.server.request.duration metric calculation

`Activity.Duration` and `http.server.request.duration` values represents the
time used to handle an inbound HTTP request as measured at the hosting layer of
ASP.NET Core. The time measurement starts once the underlying web host has:

* Sufficiently parsed the HTTP request headers on the inbound network stream to
  identify the new request.
* Initialized the context data structures such as the
  [HttpContext](https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.http.httpcontext).

The time ends when:

* The ASP.NET Core handler pipeline is finished executing.
* All response data has been sent.
* The context data structures for the request are being disposed.

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
