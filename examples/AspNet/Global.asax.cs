using System;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using OpenTelemetry;
using OpenTelemetry.Trace;

namespace Examples.AspNet
{
    public class WebApiApplication : HttpApplication
    {
        private IDisposable openTelemetry;

        protected void Application_Start()
        {
            this.openTelemetry = Sdk.CreateTracerProvider(
                 (builder) => builder
                 .AddHttpClientInstrumentation()
                 .AddAspNetInstrumentation()
                .UseJaegerExporter(c =>
                {
                    c.AgentHost = "localhost";
                    c.AgentPort = 6831;
                }));

            GlobalConfiguration.Configure(WebApiConfig.Register);

            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_End()
        {
            this.openTelemetry?.Dispose();
        }
    }
}
