// <copyright file="JaegerExporterOptions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Jaeger exporter options.
    /// OTEL_EXPORTER_JAEGER_AGENT_HOST, OTEL_EXPORTER_JAEGER_AGENT_PORT
    /// environment variables are parsed during object construction.
    /// </summary>
    /// <remarks>
    /// The constructor throws <see cref="FormatException"/> if it fails to parse
    /// any of the supported environment variables.
    /// </remarks>
    public class JaegerExporterOptions
    {
        internal const int DefaultMaxPayloadSizeInBytes = 4096;

        internal const string OTelProtocolEnvVarKey = "OTEL_EXPORTER_JAEGER_PROTOCOL";
        internal const string OTelAgentHostEnvVarKey = "OTEL_EXPORTER_JAEGER_AGENT_HOST";
        internal const string OTelAgentPortEnvVarKey = "OTEL_EXPORTER_JAEGER_AGENT_PORT";
        internal const string OTelEndpointEnvVarKey = "OTEL_EXPORTER_JAEGER_ENDPOINT";
        internal const string DefaultJaegerEndpoint = "http://localhost:14268/api/traces";

        internal static readonly Func<HttpClient> DefaultHttpClientFactory = () => new HttpClient();

        /// <summary>
        /// Initializes a new instance of the <see cref="JaegerExporterOptions"/> class.
        /// </summary>
        public JaegerExporterOptions()
            : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
        {
        }

        internal JaegerExporterOptions(IConfiguration configuration)
        {
            if (configuration.TryGetValue<JaegerExportProtocol>(
                OTelProtocolEnvVarKey,
                JaegerExporterProtocolParser.TryParse,
                out var protocol))
            {
                this.Protocol = protocol;
            }

            if (configuration.TryGetStringValue(OTelAgentHostEnvVarKey, out var agentHost))
            {
                this.AgentHost = agentHost;
            }

            if (configuration.TryGetIntValue(OTelAgentPortEnvVarKey, out var agentPort))
            {
                this.AgentPort = agentPort;
            }

            if (configuration.TryGetUriValue(OTelEndpointEnvVarKey, out var endpoint))
            {
                this.Endpoint = endpoint;
            }
        }

        /// <summary>
        /// Gets or sets the <see cref="JaegerExportProtocol"/> to use when
        /// communicating to Jaeger. Default value: <see
        /// cref="JaegerExportProtocol.UdpCompactThrift"/>.
        /// </summary>
        public JaegerExportProtocol Protocol { get; set; } = JaegerExportProtocol.UdpCompactThrift;

        /// <summary>
        /// Gets or sets the Jaeger agent host. Default value: localhost.
        /// </summary>
        public string AgentHost { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the Jaeger agent port. Default value: 6831.
        /// </summary>
        public int AgentPort { get; set; } = 6831;

        /// <summary>
        /// Gets or sets the Jaeger HTTP endpoint. Default value: "http://localhost:14268/api/traces".
        /// Typically https://jaeger-server-name:14268/api/traces.
        /// </summary>
        public Uri Endpoint { get; set; } = new Uri(DefaultJaegerEndpoint);

        /// <summary>
        /// Gets or sets the maximum payload size in bytes. Default value: 4096.
        /// </summary>
        public int? MaxPayloadSizeInBytes { get; set; } = DefaultMaxPayloadSizeInBytes;

        /// <summary>
        /// Gets or sets the export processor type to be used with Jaeger Exporter. The default value is <see cref="ExportProcessorType.Batch"/>.
        /// </summary>
        public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

        /// <summary>
        /// Gets or sets the BatchExportProcessor options. Ignored unless ExportProcessorType is BatchExporter.
        /// </summary>
        public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; } = new BatchExportActivityProcessorOptions();

        /// <summary>
        /// Gets or sets the factory function called to create the <see
        /// cref="HttpClient"/> instance that will be used at runtime to
        /// transmit spans over HTTP. The returned instance will be reused for
        /// all export invocations.
        /// </summary>
        /// <remarks>
        /// Notes:
        /// <list type="bullet">
        /// <item>This is only invoked for the <see
        /// cref="JaegerExportProtocol.HttpBinaryThrift"/> protocol.</item>
        /// <item>The default behavior when using the <see
        /// cref="JaegerExporterHelperExtensions.AddJaegerExporter(TracerProviderBuilder,
        /// Action{JaegerExporterOptions})"/> extension is if an <a
        /// href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>
        /// instance can be resolved through the application <see
        /// cref="IServiceProvider"/> then an <see cref="HttpClient"/> will be
        /// created through the factory with the name "JaegerExporter" otherwise
        /// an <see cref="HttpClient"/> will be instantiated directly.</item>
        /// </list>
        /// </remarks>
        public Func<HttpClient> HttpClientFactory { get; set; } = DefaultHttpClientFactory;
    }
}
