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
[metrics](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/metrics/semantic_conventions)
and
[traces](https://github.com/open-telemetry/opentelemetry-specification/tree/main/specification/trace/semantic_conventions).
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
dotnet add package OpenTelemetry.Instrumentation.Http
```

### Step 2: Enable HTTP Instrumentation at application startup

HTTP instrumentation must be enabled at application startup.

The following example demonstrates adding HTTP instrumentation to a console
application. This example also sets up the OpenTelemetry Console exporter, which
requires adding the package
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

For an ASP.NET Core application, adding instrumentation is typically done in the
`ConfigureServices` of your `Startup` class. Refer to documentation for
[OpenTelemetry.Instrumentation.AspNetCore](../OpenTelemetry.Instrumentation.AspNetCore/README.md).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to the documentation for
[OpenTelemetry.Instrumentation.AspNet](https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/main/src/OpenTelemetry.Instrumentation.AspNet/README.md).

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
