# ASP.NET Core Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AspNetCore.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.AspNetCore.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNetCore)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
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
`AspNetCoreInstrumentationOptions`, which allows adding [`Filter`](#filter),
[`Enrich`](#enrich) as explained below.

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
services.AddOpenTelemetryTracing(
    (builder) => builder
    .AddAspNetCoreInstrumentation(
        (options) => options.Filter =
            (httpContext) =>
            {
                // only collect telemetry about HTTP GET requests
                return httpContext.Request.Method.Equals("GET");
            })
    .AddJaegerExporter()
    );
```

It is important to note that this `Filter` option is specific to this
instrumentation. OpenTelemetry has a concept of a
[Sampler](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/sdk.md#sampling),
and the `Filter` option does the filtering *before* the Sampler is invoked.

### Enrich

This option allows one to enrich the activity with additional information
from the raw `HttpRequest`, `HttpResponse` objects. The `Enrich` action is
called only when `activity.IsAllDataRequested` is `true`. It contains the
activity itself (which can be enriched), the name of the event, and the
actual raw object.
For event name "OnStartActivity", the actual object will be `HttpRequest`.
For event name "OnStopActivity", the actual object will be `HttpResponse`

The following code snippet shows how to add additional tags using `Enrich`.

```csharp
services.AddOpenTelemetryTracing((builder) =>
{
    builder
    .AddAspNetCoreInstrumentation((options) => options.Enrich
        = (activity, eventName, rawObject) =>
    {
        if (eventName.Equals("OnStartActivity"))
        {
            if (rawObject is HttpRequest httpRequest)
            {
                activity.SetTag("requestProtocol", httpRequest.Protocol);
            }
        }
        else if (eventName.Equals("OnStopActivity"))
        {
            if (rawObject is HttpResponse httpResponse)
            {
                activity.SetTag("responseLength", httpResponse.ContentLength);
            }
        }
    })
});
```

[Processor](../../docs/trace/extending-the-sdk/README.md#processor),
is the general extensibility point to add additional properties to any activity.
The `Enrich` option is specific to this instrumentation, and is provided to
get access to `HttpRequest` and `HttpResponse`.

## References

* [Introduction to ASP.NET
  Core](https://docs.microsoft.com/aspnet/core/introduction-to-aspnet-core)
* [gRPC services using ASP.NET Core](https://docs.microsoft.com/aspnet/core/grpc/aspnetcore)
* [OpenTelemetry Project](https://opentelemetry.io/)
