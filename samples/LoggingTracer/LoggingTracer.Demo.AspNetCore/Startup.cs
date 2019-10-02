// <copyright file="Startup.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer.Demo.AspNetCore
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.AspNetCore.Hosting;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.DependencyInjection;
    using OpenTelemetry.Collector.AspNetCore;
    using OpenTelemetry.Collector.Dependencies;
    using OpenTelemetry.Hosting;

    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddOpenTelemetry(telemetry =>
            {
                telemetry.SetTracer<LoggingTracer>();
                telemetry.AddCollector<RequestsCollector>(new RequestsCollectorOptions());
                telemetry.AddCollector<DependenciesCollector>(new DependenciesCollectorOptions());
            });
        }

        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
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
