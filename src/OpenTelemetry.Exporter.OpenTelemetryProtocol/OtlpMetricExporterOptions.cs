// <copyright file="OtlpMetricExporterOptions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter
{
    public class OtlpMetricExporterOptions : ICommonOtlpExporterOptions
    {
        internal const string EndpointEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_ENDPOINT";
        internal const string HeadersEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_HEADERS";
        internal const string TimeoutEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_TIMEOUT";
        internal const string ProtocolEnvVarName = "OTEL_EXPORTER_OTLP_METRICS_PROTOCOL";

        private ICommonOtlpExporterOptions baseOptions;
        private Uri endpoint = null;
        private string headers = null;
        private int timeoutMilliseconds = int.MinValue;
        private OtlpExportProtocol protocol = OtlpExportProtocol.Unspecified;

        /// <summary>
        /// Initializes a new instance of the <see cref="OtlpMetricExporterOptions"/> class.
        /// </summary>
        internal OtlpMetricExporterOptions(ICommonOtlpExporterOptions baseOptions)
        {
            this.baseOptions = baseOptions;

            if (EnvironmentVariableHelper.LoadUri(EndpointEnvVarName, out Uri endpoint))
            {
                this.Endpoint = endpoint;
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
                    throw new FormatException($"{ProtocolEnvVarName} environment variable has an invalid value: '${protocolEnvVar}'");
                }
            }
        }

        /// <inheritdoc/>
        public Uri Endpoint
        {
            get => this.endpoint ?? this.baseOptions.Endpoint;
            set => this.endpoint = value;
        }

        /// <inheritdoc/>
        public string Headers
        {
            get => this.headers ?? this.baseOptions.Headers;
            set => this.headers = value;
        }

        /// <inheritdoc/>
        public int TimeoutMilliseconds
        {
            get => this.timeoutMilliseconds == int.MinValue ? this.baseOptions.TimeoutMilliseconds : this.timeoutMilliseconds;
            set => this.timeoutMilliseconds = value;
        }

        /// <inheritdoc/>
        public OtlpExportProtocol Protocol
        {
            get => this.protocol == OtlpExportProtocol.Unspecified ? this.baseOptions.Protocol : this.protocol;
            set => this.protocol = value;
        }

        /// <summary>
        /// Gets or sets the <see cref="MetricReaderType" /> to use. Defaults to <c>MetricReaderType.Periodic</c>.
        /// </summary>
        public MetricReaderType MetricReaderType { get; set; } = MetricReaderType.Periodic;

        /// <summary>
        /// Gets or sets the <see cref="PeriodicExportingMetricReaderOptions" /> options. Ignored unless <c>MetricReaderType</c> is <c>Periodic</c>.
        /// </summary>
        public PeriodicExportingMetricReaderOptions PeriodicExportingMetricReaderOptions { get; set; } = new PeriodicExportingMetricReaderOptions();

        /// <summary>
        /// Gets or sets the AggregationTemporality used for Histogram
        /// and Sum metrics.
        /// </summary>
        public AggregationTemporality AggregationTemporality { get; set; } = AggregationTemporality.Cumulative;
    }
}
