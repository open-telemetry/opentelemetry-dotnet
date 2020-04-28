using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using OpenTelemetry.Trace.Configuration;

namespace OpenTelemetry.Exporter.AspNet
{
    public class WebApiApplication : HttpApplication
    {
        private TracerProviderSdk tracerProvider;

        protected void Application_Start()
        {
            this.tracerProvider = TracerProviderSdk.Create(builder =>
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

            GlobalConfiguration.Configure(WebApiConfig.Register);

            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_End()
        {
            this.tracerProvider?.Dispose();
        }
    }
}
