# ASP.NET Instrumentation for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AspNet.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.AspNet.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet)

Automatically instruments the incoming requests to
[ASP.NET](https://docs.microsoft.com/aspnet/overview).

## Steps to enable OpenTelemetry.Instrumentation.AspNet

### Step1. Install Package

Add a reference to the `[OpenTelemetry.Instrumentation.AspNet](https://www.nuget.org/packages/opentelemetry.instrumentation.aspnet)` package.
Also, add any other instrumentations & exporters you will need.

```shell
dotnet add package OpenTelemetry.Instrumentation.AspNet
```

### Step2. Modify Web.config

OpenTelemetry.Instrumentation.AspNet requires adding an additional
HttpModule to your web server. The following shows changes required
to your `Web.config` when using IIS web server.

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

### Step3. Add OpenTelemetry tracing at application startup

OpenTelemetry tracing, along with the AspNet instrumentation must be enabled at
application startup. This is typically done in the `Global.asax.cs` as shown
below. This example also sets up the OpenTelemetry Jaeger exporter, which
requires adding the package `OpenTelemetry.Exporter.Jaeger` to the application.

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
            .AddJaegerExporter(jaegerOptions =>
            {
                jaegerOptions.AgentHost = "localhost";
                jaegerOptions.AgentPort = 6831;
            })
            .Build();
    }
    protected void Application_End()
    {
        this.tracerProvider?.Dispose();
    }
}
```

## Advanced configuration

`AspNetInstrumentationOptions` exposes configuration options for this instrumentation.
It currently supports configuring `Propagators`, and adding `Filter` functions.

### Configure Propagators

TODO

### Configure Filters

This instrumentation by default collects all the incoming http requests. It allows
filtering of requests by using `Filter` function in `AspNetInstrumentationOptions`.
This can be used to filter out any requests based on some condition. The Filter
receives the `HttpContext` of the incoming request, and filters out the request
if the Filter returns false or throws exception.

The following shows an example of `Filter` being used to filter out all POST requests.

```csharp
this.tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddAspNetInstrumentation(
        (options) =>
        {
            options.Filter = (httpContext) =>
            {
                // filter ot all HTTP POST requests.
                !return httpContext.Request.HttpMethod.Equals("POST");
            };
        })
    .Build();
```

### Special topic - Enriching automatically collected telemetry

AspNet instrumentation stores the `HttpRequest`, `HttpResponse` objects in the
`Activity`. These can be accessed in ActivityProcessors, and can be used to
further enrich the Activity as shown below.

The key name for HttpRequest custom property inside Activity is "OTel.AspNet.Request".
The key name for HttpResponse custom property inside Activity is "OTel.AspNet.Response".
(TODO: Expose the keys as public properties in AspNetIntrumentationOptions?)

```csharp
internal class MyAspNetEnrichingProcessor : ActivityProcessor
{
    public override void OnStart(Activity activity)
    {
        // Retrieve the HttpRequest object.
        var httpRequest = activity.GetCustomProperty("OTel.AspNetCore.Request")
                          as HttpRequest;
        if (httpRequest != null)
        {
            // Add more tags to the activity or replace an existing tag.
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
 this.tracerProvider = Sdk.CreateTracerProviderBuilder()
    .AddAspNetInstrumentation()
    .AddProcessor(new MyAspNetEnrichingProcessor())
    .AddJaegerExporter(jaegerOptions =>
    {
        jaegerOptions.AgentHost = "localhost";
        jaegerOptions.AgentPort = 6831;
    })
    .Build();
```

## References

* [ASP.NET](https://dotnet.microsoft.com/apps/aspnet)
* [OpenTelemetry Project](https://opentelemetry.io/)
