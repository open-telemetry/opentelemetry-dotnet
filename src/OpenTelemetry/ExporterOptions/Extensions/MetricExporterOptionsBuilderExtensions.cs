using System;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter
{
    public static class MetricExporterOptionsBuilderExtensions
    {
        public static TBuilder ConfigureMetricReader<TOptions, TBuilder>(
            this IMetricExporterOptionsBuilder<TOptions, TBuilder> builder, Action<IMetricExporterOptions> configure)
            where TOptions : class, IMetricExporterOptions, new()
            where TBuilder : ExporterOptionsBuilder<TOptions, TBuilder>
        {
            Guard.Null(configure);

            builder.BuilderInstance.Configure((sp, o) => configure(o));

            return builder.BuilderInstance;
        }
    }
}
