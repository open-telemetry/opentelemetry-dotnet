using System;
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

        private readonly string signalEndpointEnvVarName;
        private readonly string signalHttpEndpointPathSuffix;

        protected OtlpExporterOptionsBuilder(
            string signalEndpointEnvVarName,
            string signalHttpEndpointPathSuffix,
            string signalHeadersEnvVarName,
            string signalTimeoutEnvVarName,
            string signalProtocolEnvVarName)
        {
            this.signalEndpointEnvVarName = signalEndpointEnvVarName;
            this.signalHttpEndpointPathSuffix = signalHttpEndpointPathSuffix;

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

        protected override void ApplyTo(IServiceProvider serviceProvider, OtlpExporterOptions options)
        {
            options.Endpoint = this.BuilderOptions.Endpoint;
            options.Protocol = this.BuilderOptions.Protocol;
            options.TimeoutMilliseconds = this.BuilderOptions.TimeoutMilliseconds;

            base.ApplyTo(serviceProvider, options);

            this.AppendExportPath(options);
        }

        private void AppendExportPath(OtlpExporterOptions options)
        {
            // The exportRelativePath is only appended when the options.Endpoint property wasn't set by the user,
            // the protocol is HttpProtobuf and the OTEL_EXPORTER_OTLP_ENDPOINT environment variable
            // is present. If the user provides a custom value for options.Endpoint that value is taken as is.
            if (ReferenceEquals(options.DefaultEndpoint, options.Endpoint))
            {
                if (options.Protocol == OtlpExportProtocol.HttpProtobuf)
                {
                    if (EnvironmentVariableHelper.LoadUri(this.signalEndpointEnvVarName, out Uri endpoint)
                        || EnvironmentVariableHelper.LoadUri(EndpointEnvVarName, out endpoint))
                    {
                        // At this point we can conclude that endpoint was initialized from OTEL_EXPORTER_OTLP_ENDPOINT
                        // and has to be appended by export relative path (traces/metrics).
                        options.Endpoint = AppendPathIfNotPresent(endpoint, this.signalHttpEndpointPathSuffix);
                    }
                }
            }

            static Uri AppendPathIfNotPresent(Uri uri, string path)
            {
                var absoluteUri = uri.AbsoluteUri;
                var separator = string.Empty;

                if (absoluteUri.EndsWith("/"))
                {
                    // Endpoint already ends with 'path/'
                    if (absoluteUri.EndsWith(string.Concat(path, "/"), StringComparison.OrdinalIgnoreCase))
                    {
                        return uri;
                    }
                }
                else
                {
                    // Endpoint already ends with 'path'
                    if (absoluteUri.EndsWith(path, StringComparison.OrdinalIgnoreCase))
                    {
                        return uri;
                    }

                    separator = "/";
                }

                return new Uri(string.Concat(uri.AbsoluteUri, separator, path));
            }
        }
    }
}
