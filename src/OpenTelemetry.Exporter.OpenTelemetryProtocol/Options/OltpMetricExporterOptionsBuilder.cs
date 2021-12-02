using System;

namespace OpenTelemetry.Exporter
{
    public sealed class OltpMetricExporterOptionsBuilder : OtlpExporterOptionsBuilder<OltpMetricExporterOptionsBuilder>,
        IMetricExporterOptionsBuilder<OtlpExporterOptions, OltpMetricExporterOptionsBuilder>,
        IHttpClientFactoryExporterOptionsBuilder<OtlpExporterOptions, OltpMetricExporterOptionsBuilder>
    {
        internal const string MetricsEndpointEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT";
        internal const string MetricsHeadersEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_HEADERS";
        internal const string MetricsTimeoutEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_TIMEOUT";
        internal const string MetricsProtocolEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_PROTOCOL";
        internal const string MetricsExportPath = "v1/metrics";

        public OltpMetricExporterOptionsBuilder()
            : base(MetricsEndpointEnvVarName, MetricsExportPath, MetricsHeadersEnvVarName, MetricsTimeoutEnvVarName, MetricsProtocolEnvVarName)
        {
        }

        /// <inheritdoc/>
        public override OltpMetricExporterOptionsBuilder BuilderInstance => this;

        /// <inheritdoc/>
        protected override void ApplyTo(IServiceProvider serviceProvider, OtlpExporterOptions options)
        {
            base.ApplyTo(serviceProvider, options);

            options.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpMetricExporter");
        }
    }
}
