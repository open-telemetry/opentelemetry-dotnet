using System;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.ElasticApm
{
    public static class ElasticApmHelperExtensions
    {
        public static TracerProviderBuilder AddElasticApmExporter(
            this TracerProviderBuilder builder,
            Action<ElasticApmOptions> configure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (builder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
            {
                return deferredTracerProviderBuilder.Configure((sp, builder) =>
                {
                    AddElasticApmExporter(builder, sp.GetOptions<ElasticApmOptions>(), configure);
                });
            }

            return AddElasticApmExporter(builder, new ElasticApmOptions(), configure);
        }

        private static TracerProviderBuilder AddElasticApmExporter(
            TracerProviderBuilder builder,
            ElasticApmOptions options,
            Action<ElasticApmOptions> configure = null)
        {
            configure?.Invoke(options);

            var elasticApmExporter = new ElasticApmExporter(options);

            if (options.ExportProcessorType == ExportProcessorType.Simple)
            {
                return builder.AddProcessor(new SimpleActivityExportProcessor(elasticApmExporter));
            }
            else
            {
                return builder.AddProcessor(new BatchActivityExportProcessor(
                    elasticApmExporter,
                    options.BatchExportProcessorOptions.MaxQueueSize,
                    options.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    options.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    options.BatchExportProcessorOptions.MaxExportBatchSize));
            }
        }
    }
}
