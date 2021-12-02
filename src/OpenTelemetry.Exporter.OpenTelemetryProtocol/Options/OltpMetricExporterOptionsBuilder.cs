namespace OpenTelemetry.Exporter
{
    public sealed class OltpMetricExporterOptionsBuilder : OtlpExporterOptionsBuilder<OltpMetricExporterOptionsBuilder>, IMetricExporterOptionsBuilder<OtlpExporterOptions, OltpMetricExporterOptionsBuilder>
    {
        internal const string MetricsEndpointEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT";
        internal const string MetricsHeadersEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_HEADERS";
        internal const string MetricsTimeoutEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_TIMEOUT";
        internal const string MetricsProtocolEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_PROTOCOL";

        public OltpMetricExporterOptionsBuilder()
            : base(MetricsEndpointEnvVarName, MetricsHeadersEnvVarName, MetricsTimeoutEnvVarName, MetricsProtocolEnvVarName)
        {
        }

        /// <inheritdoc/>
        public override OltpMetricExporterOptionsBuilder BuilderInstance => this;

        /// <inheritdoc/>
        protected override void ApplyTo(OtlpExporterOptions options)
        {
            options.MetricReaderType = this.BuilderOptions.MetricReaderType;
            options.PeriodicExportingMetricReaderOptions = this.BuilderOptions.PeriodicExportingMetricReaderOptions;
            options.AggregationTemporality = this.BuilderOptions.AggregationTemporality;

            base.ApplyTo(options);
        }
    }
}
