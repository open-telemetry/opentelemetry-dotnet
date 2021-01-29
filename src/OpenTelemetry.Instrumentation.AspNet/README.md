# ASP.NET Instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AspNet.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.AspNet.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet)

This is an [Instrumentation
Library](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/glossary.md#instrumentation-library),
which instruments [ASP.NET](https://docs.microsoft.com/aspnet/overview) and
collect telemetry about incoming web requests.

## Steps to enable OpenTelemetry.Instrumentation.AspNet

### Step 1: Install Package

Add a reference to the
[`OpenTelemetry.Instrumentation.AspNet`](https://www.nuget.org/packages/opentelemetry.instrumentation.aspnet)
package. Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.AspNet
```

### Step 2: Modify Web.config

`OpenTelemetry.Instrumentation.AspNet` requires adding an additional HttpModule
to your web server. This additional HttpModule is shipped as part of
[`Microsoft.AspNet.TelemetryCorrelation`](https://www.nuget.org/packages/Microsoft.AspNet.TelemetryCorrelation/)
which is implicitly brought by `OpenTelemetry.Instrumentation.AspNet`. The
following shows changes required to your `Web.config` when using IIS web server.

```xml
<system.webServer>
    <modules>
    <add name="TelemetryCorrelationHttpModule"
    type="Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule,
    Microsoft.AspNet.TelemetryCorrelation"
    preCondition="integratedMode,managedHandler" />
    </modules>
</system.webServer>
```

### Step 3: Enable ASP.NET Instrumentation at application startup

ASP.NET instrumentation must be enabled at application startup. This is
typically done in the `Global.asax.cs` as shown below. This example also sets up
the OpenTelemetry Jaeger exporter, which requires adding the package
[`OpenTelemetry.Exporter.Jaeger`](../OpenTelemetry.Exporter.Jaeger/README.md)
to the application.

```csharp
using OpenTelemetry;
using OpenTelemetry.Trace;

public class WebApiApplication : HttpApplication
{
    private TracerProvider tracerProvider;
    protected void Application_Start()
    {
        this.tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddAspNetInstrumentation()
            .AddJaegerExporter()
            .Build();
    }
    protected void Application_End()
    {
        this.tracerProvider?.Dispose();
    }
}
```

## Advanced configuration

This instrumentation can be configured to change the default behavior by using
`AspNetInstrumentationOptions`, which allows configuring `Filter` as explained below.

### Filter

This instrumentation by default collects all the incoming http requests. It allows
filtering of requests by using the `Filter` function in `AspNetInstrumentationOptions`.
This defines the condition for allowable requests. The Filter
receives the `HttpContext` of the incoming request, and does not collect telemetry
 about the request if the Filter returns false or throws exception.

The following code snippet shows how to use `Filter` to only allow GET
requests.

```csharp
this.tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddAspNetInstrumentation(
        (options) => options.Filter =
            (httpContext) =>
            {
                // only collect telemetry about HTTP GET requests
                return httpContext.Request.HttpMethod.Equals("GET");
            })
    .Build();
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
this.tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddAspNetInstrumentation((options) => options.Enrich
        = (activity, eventName, rawObject) =>
    {
        if (eventName.Equals("OnStartActivity"))
        {
            if (rawObject is HttpRequest httpRequest)
            {
                activity.SetTag("physicalPath", httpRequest.PhysicalPath);
            }
        }
        else if (eventName.Equals("OnStopActivity"))
        {
            if (rawObject is HttpResponse httpResponse)
            {
                activity.SetTag("responseType", httpResponse.ContentType);
            }
        }
    })
    .Build();
```

[Processor](../../docs/trace/extending-the-sdk/README.md#processor),
is the general extensibility point to add additional properties to any activity.
The `Enrich` option is specific to this instrumentation, and is provided to
get access to `HttpRequest` and `HttpResponse`.

## References

* [ASP.NET](https://dotnet.microsoft.com/apps/aspnet)
* [OpenTelemetry Project](https://opentelemetry.io/)
