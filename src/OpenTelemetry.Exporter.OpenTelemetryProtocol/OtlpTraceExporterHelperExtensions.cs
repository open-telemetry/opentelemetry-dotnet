// <copyright file="OtlpTraceExporterHelperExtensions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
    /// </summary>
    public static class OtlpTraceExporterHelperExtensions
    {
        /// <summary>
        /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddOtlpExporter(this TracerProviderBuilder builder, Action<OtlpExporterOptions> configure = null)
        {
            Guard.ThrowIfNull(builder);

            if (configure != null)
            {
                builder.ConfigureServices(services => services.Configure(configure));
            }

            return builder.ConfigureBuilder((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptions<OtlpExporterOptions>>().Value;

                AddOtlpExporter(builder, options, sp);
            });
        }

        internal static TracerProviderBuilder AddOtlpExporter(
            TracerProviderBuilder builder,
            OtlpExporterOptions exporterOptions,
            IServiceProvider serviceProvider,
            Func<BaseExporter<Activity>, BaseExporter<Activity>> configureExporterInstance = null)
        {
            exporterOptions.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpTraceExporter");

            BaseExporter<Activity> otlpExporter = new OtlpTraceExporter(exporterOptions);

            if (configureExporterInstance != null)
            {
                otlpExporter = configureExporterInstance(otlpExporter);
            }

            if (exporterOptions.ExportProcessorType == ExportProcessorType.Simple)
            {
                return builder.AddProcessor(new SimpleActivityExportProcessor(otlpExporter));
            }
            else
            {
                var batchOptions = exporterOptions.BatchExportProcessorOptions ?? new();

                return builder.AddProcessor(new BatchActivityExportProcessor(
                    otlpExporter,
                    batchOptions.MaxQueueSize,
                    batchOptions.ScheduledDelayMilliseconds,
                    batchOptions.ExporterTimeoutMilliseconds,
                    batchOptions.MaxExportBatchSize));
            }
        }
    }
}
