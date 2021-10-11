# ASP.NET Telemetry HttpModule for OpenTelemetry

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule/)

The ASP.NET Telemetry HttpModule enables distributed tracing of incoming ASP.NET
requests using the OpenTelemetry API.

## Usage

### Step 1: Install NuGet package

If you are using the traditional `packages.config` reference style, a
`web.config` transform should run automatically and configure the
`TelemetryHttpModule` for you. If you are using the more modern PackageReference
style, this may be needed to be done manually. For more information, see:
[Migrate from packages.config to
PackageReference](https://docs.microsoft.com/nuget/consume-packages/migrate-packages-config-to-package-reference).

To configure your `web.config` manually, add this:

```xml
<system.webServer>
    <modules>
        <add
            name="TelemetryHttpModule"
            type="OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule,
                OpenTelemetry.Instrumentation.AspNet.TelemetryHttpModule"
            preCondition="integratedMode,managedHandler" />
    </modules>
</system.webServer>
```

### Step 2: Register a listener

`TelemetryHttpModule` registers an
[ActivitySource](https://docs.microsoft.com/dotnet/api/system.diagnostics.activitysource)
with the name `OpenTelemetry.Instrumentation.AspNet.Telemetry`. By default, .NET
`ActivitySource` will not generate any `Activity` objects unless there is a
registered listener.

To register a listener automatically using OpenTelemetry, please use the
[OpenTelemetry.Instrumentation.AspNet](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet/)
NuGet package.

To register a listener manually, use code such as the following:

```csharp
using System.Diagnostics;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using OpenTelemetry.Instrumentation.AspNet;

namespace Examples.AspNet
{
    public class WebApiApplication : HttpApplication
    {
        private ActivityListener aspNetActivityListener;

        protected void Application_Start()
        {
            this.aspNetActivityListener = new ActivityListener
            {
                ShouldListenTo = (activitySource) =>
                {
                    // Only listen to TelemetryHttpModule's ActivitySource.
                    return activitySource.Name == TelemetryHttpModule.AspNetSourceName;
                },
                Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                {
                    // Sample everything created by TelemetryHttpModule's ActivitySource.
                    return ActivitySamplingResult.AllDataAndRecorded;
                },
            };

            ActivitySource.AddActivityListener(this.aspNetActivityListener);

            GlobalConfiguration.Configure(WebApiConfig.Register);

            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_End()
        {
            this.aspNetActivityListener?.Dispose();
        }
    }
}
```

## Options

`TelemetryHttpModule` provides a static options property
(`TelemetryHttpModule.Options`) which can be used to configure the
`TelemetryHttpModule` and listen to events it fires.

### TextMapPropagator

`TextMapPropagator` controls how trace context will be extracted from incoming
Http request messages. By default, [W3C Trace
Context](https://www.w3.org/TR/trace-context/) is enabled.

The OpenTelemetry API ships with a handful of [standard
implementations](https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/src/OpenTelemetry.Api/Context/Propagation)
which may be used, or you can write your own by deriving from the
`TextMapPropagator` class.

To add support for
[Baggage](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/baggage/api.md)
propagation in addition to W3C Trace Context, use:

```csharp
TelemetryHttpModuleOptions.TextMapPropagator = new CompositeTextMapPropagator(
    new TextMapPropagator[]
    {
        new TraceContextPropagator(),
        new BaggagePropagator(),
    });
```

Note: When using the `OpenTelemetry.Instrumentation.AspNet`
`TelemetryHttpModuleOptions.TextMapPropagator` is automatically initialized to
the SDK default propagator (`Propagators.DefaultTextMapPropagator`) which by
default supports W3C Trace Context & Baggage.

### Events

`OnRequestStartedCallback`, `OnRequestStoppedCallback`, & `OnExceptionCallback`
are provided on `TelemetryHttpModuleOptions` and will be fired by the
`TelemetryHttpModule` as requests are processed.

A typical use case for these events is to add information (tags, events, and/or
links) to the created `Activity` based on the request, response, and/or
exception event being fired.
