namespace OpenTelemetry.Exporter
{
    public sealed class OltpTraceExporterOptionsBuilder : OtlpExporterOptionsBuilder<OltpTraceExporterOptionsBuilder>, ITraceExporterOptionsBuilder<OtlpExporterOptions, OltpTraceExporterOptionsBuilder>
    {
        internal const string TracesEndpointEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT";
        internal const string TracesHeadersEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_HEADERS";
        internal const string TracesTimeoutEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_TIMEOUT";
        internal const string TracesProtocolEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_PROTOCOL";

        public OltpTraceExporterOptionsBuilder()
            : base(TracesEndpointEnvVarName, TracesHeadersEnvVarName, TracesTimeoutEnvVarName, TracesProtocolEnvVarName)
        {
        }

        /// <inheritdoc/>
        public override OltpTraceExporterOptionsBuilder BuilderInstance => this;

        /// <inheritdoc/>
        protected override void ApplyTo(OtlpExporterOptions options)
        {
            options.ExportProcessorType = this.BuilderOptions.ExportProcessorType;
            options.BatchExportProcessorOptions = this.BuilderOptions.BatchExportProcessorOptions;

            base.ApplyTo(options);
        }
    }
}
