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

namespace OpenTelemetry.Exporter.Zipkin
{
    /// <summary>
    /// Zipkin trace exporter options.
    /// </summary>
    public sealed class ZipkinExporterOptions
    {
        internal const string DefaultServiceName = "OpenTelemetry Exporter";

#if !NET452
        internal const int DefaultMaxPayloadSizeInBytes = 4096;
#endif

        /// <summary>
        /// Gets or sets the name of the service reporting telemetry.
        /// </summary>
        public string ServiceName { get; set; } = DefaultServiceName;

        /// <summary>
        /// Gets or sets Zipkin endpoint address. See https://zipkin.io/zipkin-api/#/default/post_spans.
        /// Typically https://zipkin-server-name:9411/api/v2/spans.
        /// </summary>
        public Uri Endpoint { get; set; } = new Uri("http://localhost:9411/api/v2/spans");

        /// <summary>
        /// Gets or sets a value indicating whether short trace id should be used.
        /// </summary>
        public bool UseShortTraceIds { get; set; }

#if !NET452
        /// <summary>
        /// Gets or sets the maximum payload size in bytes. Default value: 4096.
        /// </summary>
        public int? MaxPayloadSizeInBytes { get; set; } = DefaultMaxPayloadSizeInBytes;
#endif

        /// <summary>
        /// Gets or sets the exporter type for Zipkin Exporter.
        /// </summary>
        public ExporterType ExporterType { get; set; } = ExporterType.BatchExportProcessor;

        /// <summary>
        /// Gets or sets get or sets the BatchExportProcessor options.
        /// </summary>
        public BatchExportProcessorOptions BatchExportProcessorOptions { get; set; } = new BatchExportProcessorOptions();
    }
}
