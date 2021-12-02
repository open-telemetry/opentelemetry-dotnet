using System;

namespace OpenTelemetry.Exporter
{
    public sealed class OltpTraceExporterOptionsBuilder : OtlpExporterOptionsBuilder<OltpTraceExporterOptionsBuilder>,
        ITraceExporterOptionsBuilder<OtlpExporterOptions, OltpTraceExporterOptionsBuilder>,
        IHttpClientFactoryExporterOptionsBuilder<OtlpExporterOptions, OltpTraceExporterOptionsBuilder>
    {
        internal const string TracesEndpointEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT";
        internal const string TracesHeadersEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_HEADERS";
        internal const string TracesTimeoutEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_TIMEOUT";
        internal const string TracesProtocolEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_PROTOCOL";
        internal const string TracesExportPath = "v1/traces";

        public OltpTraceExporterOptionsBuilder()
            : base(TracesEndpointEnvVarName, TracesExportPath, TracesHeadersEnvVarName, TracesTimeoutEnvVarName, TracesProtocolEnvVarName)
        {
        }

        /// <inheritdoc/>
        public override OltpTraceExporterOptionsBuilder BuilderInstance => this;

        /// <inheritdoc/>
        protected override void ApplyTo(IServiceProvider serviceProvider, OtlpExporterOptions options)
        {
            base.ApplyTo(serviceProvider, options);

            options.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpTraceExporter");
        }
    }
}
