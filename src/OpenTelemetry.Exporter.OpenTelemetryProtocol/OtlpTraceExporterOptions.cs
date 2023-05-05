// <copyright file="OtlpTraceExporterOptions.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol
{
    internal class OtlpTraceExporterOptions : BaseOtlpExporterOptions
    {
        internal const string TraceEndpointEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_ENDPOINT";
        internal const string TraceHeadersEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_HEADERS";
        internal const string TraceTimeoutEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_TIMEOUT";
        internal const string TraceProtocolEnvVarName = "OTEL_EXPORTER_OTLP_TRACES_PROTOCOL";

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpTraceExporterOptions"/> class.
        /// </summary>
        public OtlpTraceExporterOptions()
            : this(new ConfigurationBuilder().AddEnvironmentVariables().Build())
        {
        }

        public OtlpTraceExporterOptions(IConfiguration configuration)
        {
            Debug.Assert(configuration != null, "configuration was null");

            if (configuration.TryGetUriValue(TraceEndpointEnvVarName, out var traceEndpoint))
            {
                this.endpoint = traceEndpoint;
            }
            else if (configuration.TryGetUriValue(EndpointEnvVarName, out var endpoint))
            {
                this.endpoint = endpoint;
            }

            if (configuration.TryGetStringValue(TraceHeadersEnvVarName, out var traceHeaders))
            {
                this.Headers = traceHeaders;
            }
            else if (configuration.TryGetStringValue(HeadersEnvVarName, out var headers))
            {
                this.Headers = headers;
            }

            if (configuration.TryGetIntValue(TraceTimeoutEnvVarName, out var traceTimeout))
            {
                this.TimeoutMilliseconds = traceTimeout;
            }
            else if (configuration.TryGetIntValue(TraceTimeoutEnvVarName, out var timeout))
            {
                this.TimeoutMilliseconds = timeout;
            }

            if (configuration.TryGetValue<OtlpExportProtocol>(
                TraceProtocolEnvVarName,
                OtlpExportProtocolParser.TryParse,
                out var traceProtocol))
            {
                this.Protocol = traceProtocol;
            }
            else if (configuration.TryGetValue<OtlpExportProtocol>(
                ProtocolEnvVarName,
                OtlpExportProtocolParser.TryParse,
                out var protocol))
            {
                this.Protocol = protocol;
            }
        }
    }
}
