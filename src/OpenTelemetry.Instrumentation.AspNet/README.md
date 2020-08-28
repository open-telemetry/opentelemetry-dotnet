# ASP.NET Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AspNet.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.AspNet.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet)

Automatically instruments the incoming requests to
[ASP.NET](https://docs.microsoft.com/aspnet/overview).

## Installation

```shell
dotnet add package OpenTelemetry.Instrumentation.AspNet
```

## Configuration

Configuration with ASP.NET (.NET Framework) running in IIS or IIS Express
(if supported) to collect incoming request information.

1. Add a reference to the `OpenTelemetry.Instrumentation.AspNet` package. Add
   any other instrumentations & exporters you will need.

2. Add the TelemetryCorrelation Http module in your `Web.config`:

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

3. Configure OpenTelemetry in your application startup:

    ```csharp
    using OpenTelemetry;
    using OpenTelemetry.Trace;

    public class WebApiApplication : HttpApplication
    {
        private TracerProvider tracerProvider;
        protected void Application_Start()
        {
            this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                 .AddHttpClientInstrumentation()
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

## Filtering

This instrumentation by default collects all the incoming http requests. It allows
filtering of requests by using `Filter` function in `AspNetInstrumentationOptions`.
This can be used to filter out any requests based on some condition. The Filter
receives the `HttpContext` for the request, and filters out the request if the Filter
returns false or throws exception.

The following shows an example of Filter being used to filter out all POST requests.

```csharp
this.tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddAspNetInstrumentation(
                    (options) =>
                    {
                        options.Filter = (httpContext) =>
                        {
                            if (httpContext.Request.HttpMethod.Equals("POST"))
                            {
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        };
                    })
                .Build();
```

## References

* [ASP.NET](https://dotnet.microsoft.com/apps/aspnet)
* [OpenTelemetry Project](https://opentelemetry.io/)
