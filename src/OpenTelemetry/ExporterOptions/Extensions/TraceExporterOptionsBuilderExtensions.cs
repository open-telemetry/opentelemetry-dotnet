using System;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter
{
    public static class TraceExporterOptionsBuilderExtensions
    {
        public static TBuilder ConfigureActivityProcessor<TOptions, TBuilder>(
            this ITraceExporterOptionsBuilder<TOptions, TBuilder> builder, Action<ITraceExporterOptions> configure)
            where TOptions : class, ITraceExporterOptions, new()
            where TBuilder : ExporterOptionsBuilder<TOptions, TBuilder>
        {
            Guard.Null(configure);

            builder.BuilderInstance.Configure((sp, o) => configure(o));

            return builder.BuilderInstance;
        }
    }
}
