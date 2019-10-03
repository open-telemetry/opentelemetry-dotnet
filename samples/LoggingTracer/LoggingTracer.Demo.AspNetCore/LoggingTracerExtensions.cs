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
            services.AddSingleton<ITracerFactory, LoggingTracerFactory>();

            services.AddSingleton(Samplers.AlwaysSample);
            services.AddSingleton<RequestsCollectorOptions>();
            services.AddSingleton<RequestsCollector>();

            services.AddSingleton<DependenciesCollectorOptions>();
            services.AddSingleton<DependenciesCollector>();
        }

        internal static void UseLoggingTracer(this IApplicationBuilder app)
        {
            app.ApplicationServices.GetService<RequestsCollector>(); // get it instantiated
            app.ApplicationServices.GetService<DependenciesCollector>(); // get it instantiated
        }
    }
}
