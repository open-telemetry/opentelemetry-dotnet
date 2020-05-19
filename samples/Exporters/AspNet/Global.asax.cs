using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using OpenTelemetry.Trace.Configuration;

namespace OpenTelemetry.Exporter.AspNet
{
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

            GlobalConfiguration.Configure(WebApiConfig.Register);

            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_End()
        {
            this.tracerFactory?.Dispose();
        }
    }
}
