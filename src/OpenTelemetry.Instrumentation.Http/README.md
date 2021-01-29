# HttpClient and HttpWebRequest instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Http.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Http.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments
[System.Net.Http.HttpClient](https://docs.microsoft.com/dotnet/api/system.net.http.httpclient)
and
[System.Net.HttpWebRequest](https://docs.microsoft.com/dotnet/api/system.net.httpwebrequest)
and collects telemetry about outgoing HTTP requests.

## Steps to enable OpenTelemetry.Instrumentation.Http

### Step 1: Install Package

Add a reference to the [`OpenTelemetry.Instrumentation.Http`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.Http
```

### Step 2: Enable HTTP Instrumentation at application startup

HTTP instrumentation must be enabled at application startup.

The following example demonstrates adding HTTP instrumentation to a
console application. This example also sets up the OpenTelemetry Console
exporter, which requires adding the package
[`OpenTelemetry.Exporter.Console`](../OpenTelemetry.Exporter.Console/README.md)
to the application.

```csharp
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

For an ASP.NET Core application, adding instrumentation is typically done in
the `ConfigureServices` of your `Startup` class. Refer to documentation for
[OpenTelemetry.Instrumentation.AspNetCore](../OpenTelemetry.Instrumentation.AspNetCore/README.md).

For an ASP.NET application, adding instrumentation is typically done in the
`Global.asax.cs`. Refer to documentation for [OpenTelemetry.Instrumentation.AspNet](../OpenTelemetry.Instrumentation.AspNet/README.md).

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`HttpClientInstrumentationOptions` and
`HttpWebRequestioninstrumentationOptions`.

### SetHttpFlavor

By default, this instrumentation does not add the `http.flavor` attribute. The
`http.flavor` attribute specifies the kind of HTTP protocol used
(e.g., `1.1` for HTTP 1.1). The `SetHttpFlavor` option can be used to include
the `http.flavor` attribute.

The following example shows how to use `SetHttpFlavor`.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddHttpClientInstrumentation(
        (options) => options.SetHttpFlavor = true)
    .AddConsoleExporter()
    .Build();
```

### Filter

This instrumentation by default collects all the outgoing HTTP requests. It
allows filtering of requests by using the `Filter` function option.
This defines the condition for allowable requests. The Filter
receives the request object - `HttpRequestMessage` for .NET Core and
`HttpWebRequest` for .NET Framework - of the outgoing request and does not
collect telemetry about the request if the Filter returns false or throws
exception.

The following code snippet shows how to use `Filter` to only allow GET
requests.

```csharp
using var tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddHttpClientInstrumentation(
        (options) => options.Filter =
            (httpRequestMessage) =>
            {
                // only collect telemetry about HTTP GET requests
                return httpRequestMessage.Method.Equals(HttpMethod.Get);
            })
    .AddConsoleExporter()
    .Build();
```

It is important to note that this `Filter` option is specific to this
instrumentation. OpenTelemetry has a concept of a
[Sampler](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampling),
and the `Filter` option does the filtering *before* the Sampler is invoked.

### Enrich

This option allows one to enrich the activity with additional information
from the raw request and response objects. The `Enrich` action is
called only when `activity.IsAllDataRequested` is `true`. It contains the
activity itself (which can be enriched), the name of the event, and the
actual raw object.

#### HttpClient instrumentation

For event name "OnStartActivity", the actual object will be
`HttpRequestMessage`.

For event name "OnStopActivity", the actual object will be
`HttpResponseMessage`.

For event name "OnException", the actual object will be
`Exception`.

#### HttpWebRequest instrumentation

For event name "OnStartActivity", the actual object will be
`HttpWebRequest`.

For event name "OnStopActivity", the actual object will be
`HttpWebResponse`.

For event name "OnException", the actual object will be
`Exception`.

The following code snippet shows how to add additional tags using `Enrich`.

```csharp
services.AddOpenTelemetryTracing((builder) =>
{
    builder
    .AddHttpClientInstrumentation((options) => options.Enrich
        = (activity, eventName, rawObject) =>
    {
        if (eventName.Equals("OnStartActivity"))
        {
            if (rawObject is HttpRequestMessage request)
            {
                activity.SetTag("requestVersion", request.Version);
            }
        }
        else if (eventName.Equals("OnStopActivity"))
        {
            if (rawObject is HttpResponseMessage response)
            {
                activity.SetTag("responseVersion", response.Version);
            }
        }
        else if (eventName.Equals("OnException"))
        {
            if (rawObject is Exception exception)
            {
                activity.SetTag("stackTrace", exception.StackTrace);
            }
        }
    })
});
```

[Processor](../../docs/trace/extending-the-sdk/README.md#processor),
is the general extensibility point to add additional properties to any
activity. The `Enrich` option is specific to this instrumentation, and is
provided to get access to raw request, response, and exception objects.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
