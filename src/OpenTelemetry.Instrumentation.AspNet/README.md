# ASP.NET Instrumentation for OpenTelemetry .NET

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Instrumentation.AspNet.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Instrumentation.AspNet.svg)](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AspNet)

## Installation

```shell
dotnet add package OpenTelemetry.Instrumentation.AspNet
```

## Configuration

Configuration with ASP.NET (Full .NET Framework) running in IIS or IIS Express
(if supported) to collect incoming request information.

1. Add a reference to the `OpenTelemetry.Instrumentation.AspNet` package. Add
   any other instrumentations & exporters you will need.

2. Add the Microsoft telemetry module in your `Web.config`:

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
    public class WebApiApplication : HttpApplication
    {
        private TracerFactory tracerFactory;
        protected void Application_Start()
        {
            this.tracerFactory = TracerFactory.Create(builder =>
            {
                builder
                    .UseJaeger(c =>
                    {
                        c.AgentHost = "localhost";
                        c.AgentPort = 6831;
                    })
                    .AddRequestInstrumentation()
                    .AddDependencyInstrumentation();
            });
        }
        protected void Application_End()
        {
            this.tracerFactory?.Dispose();
        }
    }
    ```

## References

* [ASP.NET](https://dotnet.microsoft.com/apps/aspnet)
* [OpenTelemetry Project](https://opentelemetry.io/)
