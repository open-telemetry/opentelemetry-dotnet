using System;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter
{
    public static class MetricExporterOptionsBuilderExtensions
    {
        public static TBuilder ConfigureMetricReader<TOptions, TBuilder>(
            this IMetricExporterOptionsBuilder<TOptions, TBuilder> builder, Action<IMetricExporterOptions> configure)
            where TOptions : IMetricExporterOptions
        {
            Guard.Null(configure);
            configure(builder.BuilderOptions);
            return builder.BuilderInstance;
        }
    }
}
