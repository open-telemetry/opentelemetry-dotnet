﻿using System;
using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using OpenTelemetry.Trace;

namespace Examples.AspNetCore
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            this.Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "My API", Version = "v1" });

                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    c.IncludeXmlComments(xmlPath);
                }
            });

            // Switch between Zipkin/Jaeger by commenting out one of the following.

            /*
            services.AddOpenTelemetry((builder) => builder
                .AddAspNetCoreInstrumentation()
                .AddHttpInstrumentation()
                .UseJaegerActivityExporter(o =>
                {
                    o.ServiceName = this.Configuration.GetValue<string>("Jaeger:ServiceName");
                    o.AgentHost = this.Configuration.GetValue<string>("Jaeger:Host");
                    o.AgentPort = this.Configuration.GetValue<int>("Jaeger:Port");
                }));
            */

            /*
            services.AddOpenTelemetry((builder) => builder
                .AddAspNetCoreInstrumentation()
                .AddHttpInstrumentation()
                .UseZipkinExporter(o =>
                {
                    o.ServiceName = this.Configuration.GetValue<string>("Zipkin:ServiceName");
                    o.Endpoint = new Uri(this.Configuration.GetValue<string>("Zipkin:Endpoint"));
                }));
            */

            services.AddOpenTelemetry((builder) => builder
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .UseConsoleExporter());
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseSwagger();

            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
            });

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });
        }
    }
}
