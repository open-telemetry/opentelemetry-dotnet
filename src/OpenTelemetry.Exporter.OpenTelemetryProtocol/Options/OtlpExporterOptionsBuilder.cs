using System;
using System.Net.Http;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter
{
    public abstract class OtlpExporterOptionsBuilder<TBuilder> : ExporterOptionsBuilder<OtlpExporterOptions, TBuilder>
        where TBuilder : OtlpExporterOptionsBuilder<TBuilder>
    {
        internal const string EndpointEnvVarName = "OTEL_EXPORTER_OTLP_ENDPOINT";
        internal const string HeadersEnvVarName = "OTEL_EXPORTER_OTLP_HEADERS";
        internal const string TimeoutEnvVarName = "OTEL_EXPORTER_OTLP_TIMEOUT";
        internal const string ProtocolEnvVarName = "OTEL_EXPORTER_OTLP_PROTOCOL";

        protected OtlpExporterOptionsBuilder(
            string signalEndpointEnvVarName,
            string signalHeadersEnvVarName,
            string signalTimeoutEnvVarName,
            string signalProtocolEnvVarName)
        {
            if (EnvironmentVariableHelper.LoadUri(signalEndpointEnvVarName, out Uri endpoint)
                || EnvironmentVariableHelper.LoadUri(EndpointEnvVarName, out endpoint))
            {
                this.BuilderOptions.Endpoint = endpoint;
            }

            if (EnvironmentVariableHelper.LoadString(signalHeadersEnvVarName, out string headersEnvVar)
                || EnvironmentVariableHelper.LoadString(HeadersEnvVarName, out headersEnvVar))
            {
                this.BuilderOptions.Headers = headersEnvVar;
            }

            if (EnvironmentVariableHelper.LoadNumeric(signalTimeoutEnvVarName, out int timeout)
                || EnvironmentVariableHelper.LoadNumeric(TimeoutEnvVarName, out timeout))
            {
                this.BuilderOptions.TimeoutMilliseconds = timeout;
            }

            if (EnvironmentVariableHelper.LoadString(signalProtocolEnvVarName, out string protocolEnvVar)
                || EnvironmentVariableHelper.LoadString(ProtocolEnvVarName, out protocolEnvVar))
            {
                var protocol = protocolEnvVar.ToOtlpExportProtocol();
                if (protocol.HasValue)
                {
                    this.BuilderOptions.Protocol = protocol.Value;
                }
                else
                {
                    throw new FormatException($"{ProtocolEnvVarName} environment variable has an invalid value: '${protocolEnvVar}'");
                }
            }
        }

        public TBuilder ConfigureEndpoint(Uri endpoint)
        {
            this.BuilderOptions.Endpoint = endpoint;
            return this.BuilderInstance;
        }

        public TBuilder ConfigureProtocol(OtlpExportProtocol protocol)
        {
            this.BuilderOptions.Protocol = protocol;
            return this.BuilderInstance;
        }

        public TBuilder ConfigureTimeout(int timeoutMilliseconds = 10000)
        {
            this.BuilderOptions.TimeoutMilliseconds = timeoutMilliseconds;
            return this.BuilderInstance;
        }

        public TBuilder ConfigureHttpClientFactory(Func<HttpClient> httpClientFactory)
        {
            this.BuilderOptions.HttpClientFactory = httpClientFactory;
            return this.BuilderInstance;
        }

        protected override void ApplyTo(OtlpExporterOptions options)
        {
            options.Endpoint = this.BuilderOptions.Endpoint;
            options.Protocol = this.BuilderOptions.Protocol;
            options.TimeoutMilliseconds = this.BuilderOptions.TimeoutMilliseconds;
            options.HttpClientFactory = this.BuilderOptions.HttpClientFactory;

            base.ApplyTo(options);
        }
    }
}
