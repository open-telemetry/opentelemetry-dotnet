using System;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter
{
    public sealed class JaegerExporterOptionsBuilder : ExporterOptionsBuilder<JaegerExporterOptions, JaegerExporterOptionsBuilder>,
        ITraceExporterOptionsBuilder<JaegerExporterOptions, JaegerExporterOptionsBuilder>,
        IHttpClientFactoryExporterOptionsBuilder<JaegerExporterOptions, JaegerExporterOptionsBuilder>
    {
        internal const string OtelProtocolEnvVarKey = "OTEL_EXPORTER_JAEGER_PROTOCOL";
        internal const string OTelAgentHostEnvVarKey = "OTEL_EXPORTER_JAEGER_AGENT_HOST";
        internal const string OTelAgentPortEnvVarKey = "OTEL_EXPORTER_JAEGER_AGENT_PORT";
        internal const string OTelEndpointEnvVarKey = "OTEL_EXPORTER_JAEGER_ENDPOINT";

        /// <inheritdoc/>
        public override JaegerExporterOptionsBuilder BuilderInstance => this;

        public JaegerExporterOptionsBuilder()
        {
            if (EnvironmentVariableHelper.LoadString(OtelProtocolEnvVarKey, out string protocolEnvVar)
                && Enum.TryParse(protocolEnvVar, ignoreCase: true, out JaegerExportProtocol protocol))
            {
                this.BuilderOptions.Protocol = protocol;
            }

            if (EnvironmentVariableHelper.LoadString(OTelAgentHostEnvVarKey, out string agentHostEnvVar))
            {
                this.BuilderOptions.AgentHost = agentHostEnvVar;
            }

            if (EnvironmentVariableHelper.LoadNumeric(OTelAgentPortEnvVarKey, out int agentPortEnvVar))
            {
                this.BuilderOptions.AgentPort = agentPortEnvVar;
            }

            if (EnvironmentVariableHelper.LoadString(OTelEndpointEnvVarKey, out string endpointEnvVar)
                && Uri.TryCreate(endpointEnvVar, UriKind.Absolute, out Uri endpoint))
            {
                this.BuilderOptions.Endpoint = endpoint;
            }
        }

        public JaegerExporterOptionsBuilder ConfigureAgent(string agentHost = "localhost", int agentPort = 6831)
        {
            this.BuilderOptions.AgentHost = agentHost;
            this.BuilderOptions.AgentPort = agentPort;

            return this.BuilderInstance;
        }

        public JaegerExporterOptionsBuilder ConfigureEndpoint(Uri endpoint)
        {
            this.BuilderOptions.Endpoint = endpoint;
            return this.BuilderInstance;
        }

        public JaegerExporterOptionsBuilder ConfigureProtocol(JaegerExportProtocol protocol)
        {
            this.BuilderOptions.Protocol = protocol;
            return this.BuilderInstance;
        }

        public JaegerExporterOptionsBuilder ConfigureMaxPayloadSizeInBytes(int maxPayloadSizeInBytes = 4096)
        {
            this.BuilderOptions.MaxPayloadSizeInBytes = maxPayloadSizeInBytes;
            return this.BuilderInstance;
        }

        protected override void ApplyTo(IServiceProvider serviceProvider, JaegerExporterOptions options)
        {
            options.Endpoint = this.BuilderOptions.Endpoint;
            options.Protocol = this.BuilderOptions.Protocol;

            base.ApplyTo(serviceProvider, options);

            if (serviceProvider != null
                && options.Protocol == JaegerExportProtocol.HttpBinaryThrift
                && options.HttpClientFactory == JaegerExporterOptions.DefaultHttpClientFactory)
            {
                options.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpTraceExporter");
            }
        }
    }
}
