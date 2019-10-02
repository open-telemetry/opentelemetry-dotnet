// <copyright file="LoggingTracerExtensions.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer.Demo.AspNetCore
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using OpenTelemetry.Collector.AspNetCore;
    using OpenTelemetry.Collector.Dependencies;
    using OpenTelemetry.Hosting;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Sampler;

    internal static class LoggingTracerExtensions
    {
        internal static void AddLoggingTracer(this IServiceCollection services)
        {
        }
    }
}
