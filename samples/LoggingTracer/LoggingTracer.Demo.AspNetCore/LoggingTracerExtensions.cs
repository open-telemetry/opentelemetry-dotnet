// <copyright file="LoggingTracerExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer.Demo.AspNetCore
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using OpenTelemetry.Collector.AspNetCore;
    using OpenTelemetry.Collector.Dependencies;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Sampler;

    internal static class LoggingTracerExtensions
    {
        internal static void AddLoggingTracer(this IServiceCollection services)
        {
            services.AddSingleton<TracerFactoryBase, LoggingTracerFactory>();

            services.AddSingleton(Samplers.AlwaysSample);
            services.AddSingleton<AspNetCoreCollectorOptions>();
            services.AddSingleton<AspNetCoreCollector>();

            services.AddSingleton<HttpClientCollectorOptions>();
            services.AddSingleton<DependenciesCollector>();
        }

        internal static void UseLoggingTracer(this IApplicationBuilder app)
        {
            app.ApplicationServices.GetRequiredService<AspNetCoreCollector>();
            app.ApplicationServices.GetRequiredService<DependenciesCollector>();
        }
    }
}
