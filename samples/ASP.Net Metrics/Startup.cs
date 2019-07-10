using System;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Microsoft.AspNetCore;
using WebMarkupMin.AspNetCore2;
using hellocs.MetricsShim;
using hellocs.Middlewares;

namespace hellocs
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public static void Main(string[] args)
        {
            BuildWebHost(args).Run();
        }

        public static IWebHost BuildWebHost(string[] args)
        {
            return WebHost.CreateDefaultBuilder(args)
                .UseStartup<Startup>()
                .Build();
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddResponseCaching();
            services.AddMvc();
            // Add Single instance of the shim to access the metrics exporter
            services.AddSingleton<MetricShim>((p) =>
            {
                string file = File.ReadAllText(@"appsettings.json");
                return new MetricShim(file);
            });

            services.AddWebMarkupMin(options =>
            {
                options.AllowMinificationInDevelopmentEnvironment = true;
                options.DisablePoweredByHttpHeaders = true;
                options.DisableCompression = true;
            })
            .AddHtmlMinification(options =>
            {
                options.MinificationSettings.RemoveRedundantAttributes = true;
                options.MinificationSettings.RemoveHttpProtocolFromAttributes = true;
                options.MinificationSettings.RemoveHttpsProtocolFromAttributes = true;
                options.MinificationSettings.MinifyEmbeddedCssCode = false;
                options.MinificationSettings.RemoveOptionalEndTags = false;
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, MetricShim metricsExporter)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseResponseCaching();
            }

            // Add the middleware to actually provide data to the exporter
            app.UseMiddleware<MetricsMiddleware>();
            app.UseStaticFiles(new StaticFileOptions()
            {
                OnPrepareResponse = (context) =>
                {
                    var headers = context.Context.Response.GetTypedHeaders();
                    headers.CacheControl = new CacheControlHeaderValue()
                    {
                        MaxAge = TimeSpan.FromDays(365)
                    };
                }
            });

            app.UseWebMarkupMin();
            // Starts the Exporter server in a thread
            metricsExporter.Start();

            app.UseMvc(routes =>
            {
                routes.MapRoute(name: "Home", template: "{controller}/{action}/{id?}/");
            });
        }
    }
}
