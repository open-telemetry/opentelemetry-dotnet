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

        internal const string OTelAgentHostEnvVarKey = "OTEL_EXPORTER_JAEGER_AGENT_HOST";
        internal const string OTelAgentPortEnvVarKey = "OTEL_EXPORTER_JAEGER_AGENT_PORT";

        public JaegerExporterOptions()
        {
            if (EnvironmentVariableHelper.LoadString(OTelAgentHostEnvVarKey, out string agentHostEnvVar))
            {
                this.AgentHost = agentHostEnvVar;
            }

            if (EnvironmentVariableHelper.LoadNumeric(OTelAgentPortEnvVarKey, out int agentPortEnvVar))
            {
                this.AgentPort = agentPortEnvVar;
            }
        }

        /// <summary>
        /// Gets or sets the Jaeger agent host. Default value: localhost.
        /// </summary>
        public string AgentHost { get; set; } = "localhost";

        /// <summary>
        /// Gets or sets the Jaeger agent "compact thrift protocol" port. Default value: 6831.
        /// </summary>
        public int AgentPort { get; set; } = 6831;

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
    }
}
