using System;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;

namespace OpenTelemetry.Exporter.AspNet
{
    public class WebApiApplication : HttpApplication
    {
        private IDisposable openTelemetry;

        protected void Application_Start()
        {
            this.openTelemetry = OpenTelemetrySdk.EnableOpenTelemetry(
                (builder) => builder.AddDependencyInstrumentation()
                .AddRequestInstrumentation()
                .UseJaegerActivityExporter(c =>
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
