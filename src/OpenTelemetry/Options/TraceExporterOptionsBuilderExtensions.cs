using System;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter
{
    public static class TraceExporterOptionsBuilderExtensions
    {
        public static TBuilder ConfigureActivityProcessor<TOptions, TBuilder>(
            this ITraceExporterOptionsBuilder<TOptions, TBuilder> builder, Action<ITraceExporterOptions> configure)
            where TOptions : ITraceExporterOptions
        {
            Guard.Null(configure);
            configure(builder.BuilderOptions);
            return builder.BuilderInstance;
        }
    }
}
