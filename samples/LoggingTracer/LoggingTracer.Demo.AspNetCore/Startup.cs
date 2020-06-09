// <copyright file="Startup.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer.Demo.AspNetCore
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using OpenTelemetry.Instrumentation.AspNetCore;
    using OpenTelemetry.Instrumentation.Dependencies;

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOpenTelemetry(() =>
            {
                var tracerFactory = new LoggingTracerFactory();
                var tracer = tracerFactory.GetTracer("ServerApp", "semver:1.0.0");

                var dependenciesInstrumentation = new HttpClientInstrumentation();
                var aspNetCoreInstrumentation = new AspNetCoreInstrumentation();

                return tracerFactory;
            });
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.Run(async (context) =>
            {
                await context.Response.WriteAsync("Hello World!");
            });
        }
    }
}
