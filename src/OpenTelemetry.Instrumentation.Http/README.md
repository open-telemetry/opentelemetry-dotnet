# HttpClient and HttpWebRequest instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Http.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Http.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments
[System.Net.Http.HttpClient](https://docs.microsoft.com/dotnet/api/system.net.http.httpclient)
and
[System.Net.HttpWebRequest](https://docs.microsoft.com/dotnet/api/system.net.httpwebrequest)
and collects metrics and traces about outgoing HTTP requests.

**Note: This component is based on the OpenTelemetry semantic conventions for
[metrics](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/http/http-metrics.md)
and
[traces](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/http/http-spans.md).
These conventions are
[Experimental](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/document-status.md),
and hence, this package is a [pre-release](../../VERSIONING.md#pre-releases).
Until a [stable
version](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/telemetry-stability.md)
is released, there can be [breaking changes](./CHANGELOG.md). You can track the
progress from
[milestones](https://github.com/open-telemetry/opentelemetry-dotnet/milestone/23).**

## Steps to enable OpenTelemetry.Instrumentation.Http

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.Http`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package --prerelease OpenTelemetry.Instrumentation.Http
```

### Step 2: Enable HTTP Instrumentation at application startup

HTTP instrumentation must be enabled at application startup.

#### Traces

The following example demonstrates adding `HttpClient` instrumentation with the
extension method `.AddHttpClientInstrumentation()` on `TracerProviderBuilder` to
a console application. This example also sets up the OpenTelemetry Console
Exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](../OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

public class Program
{
    public static void Main(string[] args)
    {
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

#### Metrics

The following example demonstrates adding `HttpClient` instrumentation with the
extension method `.AddHttpClientInstrumentation()` on `MeterProviderBuilder` to
a console application. This example also sets up the OpenTelemetry Console
Exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](../OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Metrics;

public class Program
{
    public static void Main(string[] args)
    {
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddHttpClientInstrumentation()
            .AddConsoleExporter()
            .Build();
    }
}
```

Refer to this [example](../../examples/AspNetCore/Program.cs) to see how to
enable this instrumentation in an ASP.NET core application.

Refer to this
[example](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AspNet/README.md)
to see how to enable this instrumentation in an ASP.NET application.

#### List of metrics produced

When the application is targeting `NETFRAMEWORK`, `.NET6.0` or `NET7.0`, the instrumentation
emits the following metric.

| Name  | Details |
|-------|-----------------|
| `http.client.request.duration` | [specification](https://github.com/open-telemetry/semantic-conventions/blob/release/v1.23.x/docs/http/http-metrics.md#metric-httpclientrequestduration) |

When the application is targeting `.NET8.0` and above, the instrumentation
library enables all [built-in-metrics-system-net](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics-system-net)
by default.

Note that the `AddHttpClientInstrumentation()` extension in `.NET8.0` and above
is provided for ease of enabling all the built-in metrics via single line of
code. Alternatively, you can also enable a selected set of metrics by calling
the `AddMeter()` extension on `MeterProviderBuilder` for meters listed in
[built-in-metrics-system-net](https://learn.microsoft.com/dotnet/core/diagnostics/built-in-metrics-system-net).
If you choose to enable metrics via `AddMeter()`, then you do not need to call
`AddHttpClientInstrumentation()`. `AddHttpClientInstrumentation()` internally
also calls `AddMeter()` for the built-in meters from System.Net. There is no
difference in the features or metrics that are emitted when enabling via
`AddMeter()` versus via `AddHttpClientInstrumentation()`.

If you use `AddHttpClientInstrumentation()` and want to opt-out of metrics you
do not need, you can use
[Views](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/metrics/customizing-the-sdk#drop-an-instrument)
to do so.

 > **Note**
> http.client.request.duration metric is emitted in `seconds` as per the
semantic convention. While the convention [recommends using custom histogram
buckets](https://github.com/open-telemetry/semantic-conventions/blob/2bad9afad58fbd6b33cc683d1ad1f006e35e4a5d/docs/http/http-metrics.md)
, this feature is not yet available via .NET Metrics API. A
[workaround](https://github.com/open-telemetry/opentelemetry-dotnet/pull/4820)
has been included in OTel SDK starting version `1.6.0` which applies recommended
buckets by default for `http.client.request.duration`. This applies to all
targeted frameworks.

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`HttpClientInstrumentationOptions`. It is important to note that there are
differences between .NET Framework and newer .NET/.NET Core runtimes which
govern what options are used. On .NET Framework, `HttpClient` uses the
`HttpWebRequest` API. On .NET & .NET Core, `HttpWebRequest` uses the
`HttpClient` API. As such, depending on the runtime, only one half of the
"filter" & "enrich" options are used.

### .NET & .NET Core

#### Filter HttpClient API

This instrumentation by default collects all the outgoing HTTP requests. It
allows filtering of requests by using the `FilterHttpRequestMessage` function
option. This defines the condition for allowable requests. The filter function
receives the request object (`HttpRequestMessage`) representing the outgoing
request and does not collect telemetry about the request if the filter function
returns `false` or throws an exception.

The following code snippet shows how to use `FilterHttpRequestMessage` to only
allow GET requests.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddHttpClientInstrumentation(
        // Note: Only called on .NET & .NET Core runtimes.
        (options) => options.FilterHttpRequestMessage =
            (httpRequestMessage) =>
            {
                // Example: Only collect telemetry about HTTP GET requests.
                return httpRequestMessage.Method.Equals(HttpMethod.Get);
            })
    .AddConsoleExporter()
    .Build();
```

It is important to note that this `FilterHttpRequestMessage` option is specific
to this instrumentation. OpenTelemetry has a concept of a
[Sampler](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampling),
and the `FilterHttpRequestMessage` option does the filtering *after* the Sampler
is invoked.

#### Enrich HttpClient API

This instrumentation library provides options that can be used to
enrich the activity with additional information. These actions are called
only when `activity.IsAllDataRequested` is `true`. It contains the activity
itself (which can be enriched) and the actual raw object.

`HttpClientInstrumentationOptions` provides 3 enrich options:
`EnrichWithHttpRequestMessage`, `EnrichWithHttpResponseMessage` and
`EnrichWithException`. These are based on the raw object that is passed in to
the action to enrich the activity.

Example:

```csharp
using System.Net.Http;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddHttpClientInstrumentation((options) =>
    {
        // Note: Only called on .NET & .NET Core runtimes.
        options.EnrichWithHttpRequestMessage = (activity, httpRequestMessage) =>
        {
            activity.SetTag("requestVersion", httpRequestMessage.Version);
        };
        // Note: Only called on .NET & .NET Core runtimes.
        options.EnrichWithHttpResponseMessage = (activity, httpResponseMessage) =>
        {
            activity.SetTag("responseVersion", httpResponseMessage.Version);
        };
        // Note: Called for all runtimes.
        options.EnrichWithException = (activity, exception) =>
        {
            activity.SetTag("stackTrace", exception.StackTrace);
        };
    })
    .Build();
```

### .NET Framework

#### Filter HttpWebRequest API

This instrumentation by default collects all the outgoing HTTP requests. It
allows filtering of requests by using the `FilterHttpWebRequest` function
option. This defines the condition for allowable requests. The filter function
receives the request object (`HttpWebRequest`) representing the outgoing request
and does not collect telemetry about the request if the filter function returns
`false` or throws an exception.

The following code snippet shows how to use `FilterHttpWebRequest` to only allow
GET requests.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddHttpClientInstrumentation(
        // Note: Only called on .NET Framework.
        (options) => options.FilterHttpWebRequest =
            (httpWebRequest) =>
            {
                // Example: Only collect telemetry about HTTP GET requests.
                return httpWebRequest.Method.Equals(HttpMethod.Get.Method);
            })
    .AddConsoleExporter()
    .Build();
```

It is important to note that this `FilterHttpWebRequest` option is specific to
this instrumentation. OpenTelemetry has a concept of a
[Sampler](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampling),
and the `FilterHttpWebRequest` option does the filtering *after* the Sampler is
invoked.

#### Enrich HttpWebRequest API

This instrumentation library provides options that can be used to
enrich the activity with additional information. These actions are called
only when `activity.IsAllDataRequested` is `true`. It contains the activity
itself (which can be enriched) and the actual raw object.

`HttpClientInstrumentationOptions` provides 3 enrich options:
`EnrichWithHttpWebRequest`, `EnrichWithHttpWebResponse` and
`EnrichWithException`. These are based on the raw object that is passed in to
the action to enrich the activity.

Example:

```csharp
using System.Net;

var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddHttpClientInstrumentation((options) =>
    {
        // Note: Only called on .NET Framework.
        options.EnrichWithHttpWebRequest = (activity, httpWebRequest) =>
        {
            activity.SetTag("requestVersion", httpWebRequest.Version);
        };
        // Note: Only called on .NET Framework.
        options.EnrichWithHttpWebResponse = (activity, httpWebResponse) =>
        {
            activity.SetTag("responseVersion", httpWebResponse.Version);
        };
        // Note: Called for all runtimes.
        options.EnrichWithException = (activity, exception) =>
        {
            activity.SetTag("stackTrace", exception.StackTrace);
        };
    })
    .Build();
```

[Processor](../../docs/trace/extending-the-sdk/README.md#processor), is the
general extensibility point to add additional properties to any activity. The
`Enrich` option is specific to this instrumentation, and is provided to get
access to raw request, response, and exception objects.

### RecordException

This instrumentation automatically sets Activity Status to Error if the Http
StatusCode is >= 400. Additionally, `RecordException` feature may be turned on,
to store the exception to the Activity itself as ActivityEvent.

## Troubleshooting

This component uses an
[EventSource](https://docs.microsoft.com/dotnet/api/system.diagnostics.tracing.eventsource)
with the name "OpenTelemetry-Instrumentation-Http" for its internal logging.
Please refer to [SDK
troubleshooting](../OpenTelemetry/README.md#troubleshooting) for instructions on
seeing these internal logs.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
