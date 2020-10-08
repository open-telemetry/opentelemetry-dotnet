# ASP.NET Core Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AspNetCore.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.AspNetCore.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/glossary.md#instrumentation-library),
which instruments [ASP.NET Core](https://docs.microsoft.com/aspnet/core) and
collect telemetry about incoming web requests.
This instrumentation also collects incoming gRPC requests using
[Grpc.AspNetCore](https://www.nuget.org/packages/Grpc.AspNetCore).

## Steps to enable OpenTelemetry.Instrumentation.AspNetCore

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.AspNetCore`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.AspNetCore
```

### Step 2: Enable ASP.NET Core Instrumentation at application startup

ASP.NET Core instrumentation must be enabled at application startup. This is
typically done in the `ConfigureServices` of your `Startup` class. The example
below enables this instrumentation by using an extension method on
`IServiceCollection`. This extension method requires adding the package
[`OpenTelemetry.Extensions.Hosting`](../OpenTelemetry.Extensions.Hosting/README.md)
to the application. This ensures the instrumentation is disposed when the host
is shutdown.

Additionally, this examples sets up the OpenTelemetry Jaeger exporter, which
requires adding the package
[`OpenTelemetry.Exporter.Jaeger`](../OpenTelemetry.Exporter.Jaeger/README.md) to
the application.

```csharp
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Trace;

public void ConfigureServices(IServiceCollection services)
{
    services.AddOpenTelemetryTracing(
        (builder) => builder
                .AddAspNetCoreInstrumentation()
                .AddJaegerExporter()
                );
}
```

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`AspNetCoreInstrumentationOptions`, which allows configuring
[`Propagator`](#propagator) and [`Filter`](#filter) as explained below.

### Propagator

TODO

### Filter

This instrumentation by default collects all the incoming http requests. It
allows filtering of requests by using `Filter` function in
`AspNetCoreInstrumentationOptions`. This can be used to filter out any requests
based on some condition. The Filter receives the `HttpContext` of the incoming
request, and filters out the request if the Filter returns false or throws
exception.

The following shows an example of `Filter` being used to filter out all POST
requests.

```csharp
services.AddOpenTelemetryTracing(
        (builder) => builder
        .AddAspNetCoreInstrumentation(
            opt => opt.Filter =
                (httpContext) =>
                {
                    // filter out all HTTP POST requests.
                    return !httpContext.Request.Method.Equals("POST");
                })
        .AddJaegerExporter()
        );
```

It is important to note that this `Filter` option is specific to this
instrumentation. OpenTelemetry has a concept of
[Sampler](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/sdk.md#sampling),
and the `Filter` option does the filtering *before* the Sampler is invoked.

### Special topic - Enriching automatically collected telemetry

This instrumentation library stores the raw `HttpRequest`, `HttpResponse`
objects in the activity. This can be accessed in `BaseProcessor<Activity>`, and
can be used to further enrich the Activity with additional tags as shown below.

The key name for HttpRequest custom property inside Activity is
"OTel.AspNetCore.Request".

The key name for HttpResponse custom property inside
Activity is "OTel.AspNetCore.Response".

```csharp
internal class MyAspNetCoreEnrichingProcessor : BaseProcessor<Activity>
{
    public override void OnStart(Activity activity)
    {
        // Retrieve the HttpRequest object.
        var httpRequest = activity.GetCustomProperty("OTel.AspNetCore.Request")
                          as HttpRequest;
        if (httpRequest != null)
        {
            // Add more tags to the activity
            activity.SetTag("mycustomtag", httpRequest.Headers["myheader"]);
        }
    }

    public override void OnEnd(Activity activity)
    {
        // Retrieve the HttpResponse object.
        var httpResponse = activity.GetCustomProperty("OTel.AspNetCore.Response")
                           as HttpResponse;
        if (httpResponse != null)
        {
            var statusCode = httpResponse.StatusCode;
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
services.AddOpenTelemetryTracing(
    (builder) => builder
                .AddAspNetCoreInstrumentation()
                .AddProcessor(new MyAspNetCoreEnrichingProcessor())
                );
```

## References

* [Introduction to ASP.NET
  Core](https://docs.microsoft.com/aspnet/core/introduction-to-aspnet-core)
* [gRPC services using ASP.NET Core](https://docs.microsoft.com/aspnet/core/grpc/aspnetcore)
* [OpenTelemetry Project](https://opentelemetry.io/)
