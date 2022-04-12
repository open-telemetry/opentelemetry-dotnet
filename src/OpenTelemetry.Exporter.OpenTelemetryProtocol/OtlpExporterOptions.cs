// <copyright file="OtlpExporterOptions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// OpenTelemetry Protocol (OTLP) exporter options.
    /// OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_HEADERS, OTEL_EXPORTER_OTLP_TIMEOUT, OTEL_EXPORTER_OTLP_PROTOCOL
    /// environment variables are parsed during object construction.
    /// </summary>
    /// <remarks>
    /// The constructor throws <see cref="FormatException"/> if it fails to parse
    /// any of the supported environment variables.
    /// </remarks>
    public class OtlpExporterOptions
    {
        internal const string EndpointEnvVarName = "OTEL_EXPORTER_OTLP_ENDPOINT";
        internal const string HeadersEnvVarName = "OTEL_EXPORTER_OTLP_HEADERS";
        internal const string TimeoutEnvVarName = "OTEL_EXPORTER_OTLP_TIMEOUT";
        internal const string ProtocolEnvVarName = "OTEL_EXPORTER_OTLP_PROTOCOL";

        internal readonly Func<HttpClient> DefaultHttpClientFactory;

        private const string DefaultGrpcEndpoint = "http://localhost:4317";
        private const string DefaultHttpEndpoint = "http://localhost:4318";
        private const OtlpExportProtocol DefaultOtlpExportProtocol = OtlpExportProtocol.Grpc;

        private Uri endpoint;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpExporterOptions"/> class.
        /// </summary>
        public OtlpExporterOptions()
        {
            if (EnvironmentVariableHelper.LoadUri(EndpointEnvVarName, out Uri parsedEndpoint))
            {
                this.endpoint = parsedEndpoint;
            }

            if (EnvironmentVariableHelper.LoadString(HeadersEnvVarName, out string headersEnvVar))
            {
                this.Headers = headersEnvVar;
            }

            if (EnvironmentVariableHelper.LoadNumeric(TimeoutEnvVarName, out int timeout))
            {
                this.TimeoutMilliseconds = timeout;
            }

            if (EnvironmentVariableHelper.LoadString(ProtocolEnvVarName, out string protocolEnvVar))
            {
                var protocol = protocolEnvVar.ToOtlpExportProtocol();
                if (protocol.HasValue)
                {
                    this.Protocol = protocol.Value;
                }
                else
                {
                    throw new FormatException($"{ProtocolEnvVarName} environment variable has an invalid value: '{protocolEnvVar}'");
                }
            }

            this.HttpClientFactory = this.DefaultHttpClientFactory = () =>
            {
                return new HttpClient
                {
                    Timeout = TimeSpan.FromMilliseconds(this.TimeoutMilliseconds),
                };
            };
        }

        /// <summary>
        /// Gets or sets the target to which the exporter is going to send telemetry.
        /// Must be a valid Uri with scheme (http or https) and host, and
        /// may contain a port and path. The default value is
        /// * http://localhost:4317 for <see cref="OtlpExportProtocol.Grpc"/>
        /// * http://localhost:4318 for <see cref="OtlpExportProtocol.HttpProtobuf"/>.
        /// </summary>
        public Uri Endpoint
        {
            get
            {
                if (this.endpoint == null)
                {
                    this.endpoint = this.Protocol == OtlpExportProtocol.Grpc
                        ? new Uri(DefaultGrpcEndpoint)
                        : new Uri(DefaultHttpEndpoint);
                }

                return this.endpoint;
            }

            set
            {
                this.endpoint = value;
                this.ProgrammaticallyModifiedEndpoint = true;
            }
        }

        /// <summary>
        /// Gets or sets optional headers for the connection. Refer to the <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#specifying-headers-via-environment-variables">
        /// specification</a> for information on the expected format for Headers.
        /// </summary>
        public string Headers { get; set; }

        /// <summary>
        /// Gets or sets the max waiting time (in milliseconds) for the backend to process each batch. The default value is 10000.
        /// </summary>
        public int TimeoutMilliseconds { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the the OTLP transport protocol. Supported values: Grpc and HttpProtobuf.
        /// </summary>
        public OtlpExportProtocol Protocol { get; set; } = DefaultOtlpExportProtocol;

        /// <summary>
        /// Gets or sets the export processor type to be used with the OpenTelemetry Protocol Exporter. The default value is <see cref="ExportProcessorType.Batch"/>.
        /// </summary>
        public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

        /// <summary>
        /// Gets or sets the BatchExportProcessor options. Ignored unless ExportProcessorType is Batch.
        /// </summary>
        public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; } = new BatchExportActivityProcessorOptions();

        /// <summary>
        /// Gets or sets the factory function called to create the <see
        /// cref="HttpClient"/> instance that will be used at runtime to
        /// transmit telemetry over HTTP. The returned instance will be reused
        /// for all export invocations.
        /// </summary>
        /// <remarks>
        /// Notes:
        /// <list type="bullet">
        /// <item>This is only invoked for the <see
        /// cref="OtlpExportProtocol.HttpProtobuf"/> protocol.</item>
        /// <item>The default behavior when using the <see
        /// cref="OtlpTraceExporterHelperExtensions.AddOtlpExporter(TracerProviderBuilder,
        /// Action{OtlpExporterOptions})"/> extension is if an <a
        /// href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>
        /// instance can be resolved through the application <see
        /// cref="IServiceProvider"/> then an <see cref="HttpClient"/> will be
        /// created through the factory with the name "OtlpTraceExporter"
        /// otherwise an <see cref="HttpClient"/> will be instantiated
        /// directly.</item>
        /// <item>The default behavior when using the <see
        /// cref="OtlpMetricExporterExtensions.AddOtlpExporter(MeterProviderBuilder,
        /// Action{OtlpExporterOptions})"/> extension is if an <a
        /// href="https://docs.microsoft.com/dotnet/api/system.net.http.ihttpclientfactory">IHttpClientFactory</a>
        /// instance can be resolved through the application <see
        /// cref="IServiceProvider"/> then an <see cref="HttpClient"/> will be
        /// created through the factory with the name "OtlpMetricExporter"
        /// otherwise an <see cref="HttpClient"/> will be instantiated
        /// directly.</item>
        /// </list>
        /// </remarks>
        public Func<HttpClient> HttpClientFactory { get; set; }

        /// <summary>
        /// Gets a value indicating whether <see cref="Endpoint" /> was modified via its setter.
        /// </summary>
        internal bool ProgrammaticallyModifiedEndpoint { get; private set; }
    }
}
