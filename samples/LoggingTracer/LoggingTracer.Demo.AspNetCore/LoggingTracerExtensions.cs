namespace LoggingTracer.Demo.AspNetCore
{
    using Microsoft.AspNetCore.Builder;
    using Microsoft.Extensions.DependencyInjection;
    using OpenTelemetry.Collector.AspNetCore;
    using OpenTelemetry.Collector.Dependencies;
    using OpenTelemetry.Trace;
    using OpenTelemetry.Trace.Sampler;

    static class LoggingTracerExtensions {
        internal static void AddLoggingTracer(this IServiceCollection services)
        {
            services.AddSingleton<ITracerFactory>(new global::LoggingTracer.LoggingTracerFactory());

            services.AddSingleton<ISampler>(Samplers.AlwaysSample);
            services.AddSingleton<RequestsCollectorOptions>(new RequestsCollectorOptions());
            services.AddSingleton<RequestsCollector>();

            services.AddSingleton<DependenciesCollectorOptions>(new DependenciesCollectorOptions());
            services.AddSingleton<DependenciesCollector>();
        }

        internal static void UseLoggingTracer(this IApplicationBuilder app)
        {
            app.ApplicationServices.GetService<RequestsCollector>(); // get it instantiated
            app.ApplicationServices.GetService<DependenciesCollector>(); // get it instantiated
        }
    }
}
