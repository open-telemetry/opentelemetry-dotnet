// <copyright file="ZipkinExporterHelperExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Zipkin;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of Zipkin exporter.
    /// </summary>
    public static class ZipkinExporterHelperExtensions
    {
        /// <summary>
        /// Adds Zipkin exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The objects should not be disposed.")]
        public static TracerProviderBuilder AddZipkinExporter(this TracerProviderBuilder builder, Action<ZipkinExporterOptions> configure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var exporterOptions = new ZipkinExporterOptions();
            configure?.Invoke(exporterOptions);
            var zipkinExporter = new ZipkinExporter(exporterOptions);

            if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple)
            {
                return builder.AddProcessor(new SimpleActivityExportProcessor(zipkinExporter));
            }
            else
            {
                return builder.AddProcessor(new BatchActivityExportProcessor(
                    zipkinExporter,
                    exporterOptions.BatchExportProcessorOptions.MaxQueueSize,
                    exporterOptions.BatchExportProcessorOptions.ScheduledDelayMilliseconds,
                    exporterOptions.BatchExportProcessorOptions.ExporterTimeoutMilliseconds,
                    exporterOptions.BatchExportProcessorOptions.MaxExportBatchSize));
            }
        }
    }
}
