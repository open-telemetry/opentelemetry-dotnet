# HttpClient and HttpWebRequest instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.Http.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.Http.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.Http)

This is an
[Instrumentation Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library),
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
        using Sdk.CreateTracerProviderBuilder()
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

### Propagator

TODO

### SetHttpFlavor

By default, this instrumentation does not add the `http.flavor` attribute. The
`http.flavor` attribute specifies the kind of HTTP protocol used
(e.g., `1.1` for HTTP 1.1). The `SetHttpFlavor` option can be used to include
the `http.flavor` attribute.

The following example shows how to use `SetHttpFlavor`.

```csharp
using Sdk.CreateTracerProviderBuilder()
    .AddHttpClientInstrumentation(
        options => options.SetHttpFlavor = true)
    .AddConsoleExporter()
    .Build();
```

### Filter

This instrumentation by default collects all the outgoing HTTP requests. It
allows filtering of requests by using the `Filter` function option.
This can be used to filter out any requests based on some condition. The Filter
receives the request object - `HttpRequestMessage` for .NET Core and
`HttpWebRequest` for .NET Framework - of the outgoing request and filters out
the request if the Filter returns false or throws an exception.

The following shows an example of `Filter` being used to filter out all POST
requests.

```csharp
using Sdk.CreateTracerProviderBuilder()
    .AddHttpClientInstrumentation(
        options => options.Filter =
            httpRequestMessage =>
            {
                // filter out all HTTP POST requests.
                return httpRequestMessage.Method != HttpMethod.Post;
            })
    .AddConsoleExporter()
    .Build();
```

It is important to note that this `Filter` option is specific to this
instrumentation. OpenTelemetry has a concept of a
[Sampler](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#sampling),
and the `Filter` option does the filtering *before* the Sampler is invoked.

### Special topic - Enriching automatically collected telemetry

This instrumentation library stores the raw request, response and any exception
object in the activity. These can be accessed in ActivityProcessors, and can be
used to further enrich the Activity with additional tags as shown below.

#### HttpClient instrumentation

The key name for HttpRequestMessage custom property inside Activity is
"OTel.HttpHandler.Request".

The key name for HttpResponseMessage custom property inside Activity is
"OTel.HttpHandler.Response".

The key name for Exception custom property inside Activity is
"OTel.HttpHandler.Exception".

#### HttpWebRequest instrumentation

The key name for HttpWebRequest custom property inside Activity is
"OTel.HttpWebRequest.Request".

The key name for HttpWebResponse custom property inside Activity is
"OTel.HttpWebRequest.Response".

The key name for Exception custom property inside Activity is
"OTel.HttpWebRequest.Exception".

```csharp
internal class MyHttpEnrichingProcessor : ActivityProcessor
{
    public override void OnStart(Activity activity)
    {
        // Retrieve the HttpRequestMessage object.
        var request = activity.GetCustomProperty("OTel.HttpHandler.Request")
                          as HttpRequestMessage;
        if (request != null)
        {
            // Add more tags to the activity
            activity.SetTag("mycustomtag", request.Headers["myheader"]);
        }
    }

    public override void OnEnd(Activity activity)
    {
        // Retrieve the HttpResponseMessage object.
        var response = activity.GetCustomProperty("OTel.HttpHandler.Response")
                           as HttpResponseMessage;
        if (response != null)
        {
            var statusCode = response.StatusCode;
            bool success = statusCode < 400;
            // Add more tags to the activity or replace an existing tag.
            activity.SetTag("myCustomSuccess", success);
        }
    }
}
```

The custom processor must be added to the provider as below. It is important to
add the enrichment processor before any exporters so that exporters see the
changes done by them.

```csharp
using Sdk.CreateTracerProviderBuilder()
    .AddProcessor(new MyHttpEnrichingProcessor())
    .AddConsoleExporter()
    .Build();
```

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
