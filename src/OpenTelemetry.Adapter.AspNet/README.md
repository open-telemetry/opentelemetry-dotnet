# ASP.NET adapter

Configuration with ASP.NET (Full .NET Framework) running in IIS or IIS Express (if supported) to collect incoming request information.

1. Add a reference to the `OpenTelemetry.Adapter.AspNet` package. Add any other adapters & exporters you will need.

2. Add the Microsoft telemetry module in your `Web.config`:

    ```xml	
    <system.webServer>	
        <modules>	
          <add name="TelemetryCorrelationHttpModule" type="Microsoft.AspNet.TelemetryCorrelation.TelemetryCorrelationHttpModule, Microsoft.AspNet.TelemetryCorrelation" preCondition="integratedMode,managedHandler"/>	
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
                    .AddRequestAdapter()	
                    .AddDependencyAdapter();	
            });	
        }	
        protected void Application_End()	
        {	
            this.tracerFactory?.Dispose();	
        }	
    }	
    ```