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
using System.Security;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Configuration options for the OpenTelemetry Protocol (OTLP) exporter.
    /// </summary>
    public class OtlpExporterOptions
    {
        internal const string EndpointEnvVarName = "OTEL_EXPORTER_OTLP_ENDPOINT";
        internal const string TracesEndpointEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT";
        internal const string MetricsEndpointEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT";
        internal const string HeadersEnvVarName = "OTEL_EXPORTER_OTLP_HEADERS";
        internal const string TimeoutEnvVarName = "OTEL_EXPORTER_OTLP_TIMEOUT";
        internal const string ProtocolEnvVarName = "OTEL_EXPORTER_OTLP_PROTOCOL";

        internal const string TraceExportPath = "v1/traces";
        internal const string MetricsExportPath = "v1/metrics";

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpExporterOptions"/> class.
        /// </summary>
        public OtlpExporterOptions()
        {
            try
            {
                // Protocol initialization should come before endpoints as it's initialization logic depends on the protocol.
                var protocolEnvVar = Environment.GetEnvironmentVariable(ProtocolEnvVarName);
                if (!string.IsNullOrEmpty(protocolEnvVar))
                {
                    var protocol = protocolEnvVar.ToExportProtocol();
                    if (protocol.HasValue)
                    {
                        this.Protocol = protocol.Value;
                    }
                    else
                    {
                        OpenTelemetryProtocolExporterEventSource.Log.UnsupportedProtocol(protocolEnvVar);
                    }
                }

                this.InitializeEndpoints(this.Protocol);

                string headersEnvVar = Environment.GetEnvironmentVariable(HeadersEnvVarName);
                if (!string.IsNullOrEmpty(headersEnvVar))
                {
                    this.Headers = headersEnvVar;
                }

                string timeoutEnvVar = Environment.GetEnvironmentVariable(TimeoutEnvVarName);
                if (!string.IsNullOrEmpty(timeoutEnvVar))
                {
                    if (int.TryParse(timeoutEnvVar, out var timeout))
                    {
                        this.TimeoutMilliseconds = timeout;
                    }
                    else
                    {
                        OpenTelemetryProtocolExporterEventSource.Log.FailedToParseEnvironmentVariable(TimeoutEnvVarName, timeoutEnvVar);
                    }
                }
            }
            catch (SecurityException ex)
            {
                // The caller does not have the required permission to
                // retrieve the value of an environment variable from the current process.
                OpenTelemetryProtocolExporterEventSource.Log.MissingPermissionsToReadEnvironmentVariable(ex);
            }
        }

        /// <summary>
        /// Gets or sets the target to which the exporter is going to send traces or metrics.
        /// Must be a valid Uri with scheme (http) and host, and
        /// may contain a port and path. Secure connection(https) is not
        /// supported.
        /// </summary>
        public Uri Endpoint { get; set; } = new Uri("http://localhost:4317");

        /// <summary>
        /// Gets or sets the target to which the exporter is going to send traces.
        /// Must be a valid Uri with scheme (http) and host, and
        /// may contain a port and path. Secure connection(https) is not
        /// supported.
        /// </summary>
        public Uri TracesEndpoint { get; set; } = new Uri("http://localhost:4317");

        /// <summary>
        /// Gets or sets the target to which the exporter is going to send metrics.
        /// Must be a valid Uri with scheme (http) and host, and
        /// may contain a port and path. Secure connection(https) is not
        /// supported.
        /// </summary>
        public Uri MetricsEndpoint { get; set; } = new Uri("http://localhost:4317");

        /// <summary>
        /// Gets or sets optional headers for the connection. Refer to the <a href="https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/protocol/exporter.md#specifying-headers-via-environment-variables">
        /// specification</a> for information on the expected format for Headers.
        /// </summary>
        public string Headers { get; set; }

        /// <summary>
        /// Gets or sets the max waiting time (in milliseconds) for the backend to process each span batch. The default value is 10000.
        /// </summary>
        public int TimeoutMilliseconds { get; set; } = 10000;

        /// <summary>
        /// Gets or sets the the OTLP transport protocol. Supported values: grpc, http/protobuf.
        /// </summary>
        public ExportProtocol Protocol { get; set; } = ExportProtocol.Grpc;

        /// <summary>
        /// Gets or sets the export processor type to be used with the OpenTelemetry Protocol Exporter. The default value is <see cref="ExportProcessorType.Batch"/>.
        /// </summary>
        public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

        /// <summary>
        /// Gets or sets the BatchExportProcessor options. Ignored unless ExportProcessorType is Batch.
        /// </summary>
        public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; } = new BatchExportActivityProcessorOptions();

        /// <summary>
        /// Gets or sets the metric export interval in milliseconds. The default value is 1000 milliseconds.
        /// </summary>
        public int MetricExportIntervalMilliseconds { get; set; } = 1000;

        /// <summary>
        /// Gets or sets the AggregationTemporality used for Histogram
        /// and Sum metrics.
        /// </summary>
        public AggregationTemporality AggregationTemporality { get; set; } = AggregationTemporality.Cumulative;
    }
}
