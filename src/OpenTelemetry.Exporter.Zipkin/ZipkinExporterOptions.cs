// <copyright file="ZipkinExporterOptions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Zipkin.Implementation;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Zipkin trace exporter options.
    /// </summary>
    public sealed class ZipkinExporterOptions
    {
        internal const int DefaultMaxPayloadSizeInBytes = 4096;
        internal const string ZipkinEndpointEnvVar = "OTEL_EXPORTER_ZIPKIN_ENDPOINT";
        internal const string DefaultZipkinEndpoint = "http://localhost:9411/api/v2/spans";

        /// <summary>
        /// Initializes a new instance of the <see cref="ZipkinExporterOptions"/> class.
        /// Initializes zipkin endpoint.
        /// </summary>
        public ZipkinExporterOptions()
        {
            try
            {
                this.Endpoint = new Uri(Environment.GetEnvironmentVariable(ZipkinEndpointEnvVar) ?? DefaultZipkinEndpoint);
            }
            catch (Exception ex)
            {
                this.Endpoint = new Uri(DefaultZipkinEndpoint);
                ZipkinExporterEventSource.Log.FailedEndpointInitialization(ex);
            }
        }

        /// <summary>
        /// Gets or sets Zipkin endpoint address. See https://zipkin.io/zipkin-api/#/default/post_spans.
        /// Typically https://zipkin-server-name:9411/api/v2/spans.
        /// </summary>
        public Uri Endpoint { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether short trace id should be used.
        /// </summary>
        public bool UseShortTraceIds { get; set; }

        /// <summary>
        /// Gets or sets the maximum payload size in bytes. Default value: 4096.
        /// </summary>
        public int? MaxPayloadSizeInBytes { get; set; } = DefaultMaxPayloadSizeInBytes;

        /// <summary>
        /// Gets or sets the export processor type to be used with Zipkin Exporter. The default value is <see cref="ExportProcessorType.Batch"/>.
        /// </summary>
        public ExportProcessorType ExportProcessorType { get; set; } = ExportProcessorType.Batch;

        /// <summary>
        /// Gets or sets the BatchExportProcessor options. Ignored unless ExportProcessorType is BatchExporter.
        /// </summary>
        public BatchExportProcessorOptions<Activity> BatchExportProcessorOptions { get; set; } = new BatchExportProcessorOptions<Activity>();
    }
}
