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
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
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
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddOtlpExporter(this TracerProviderBuilder builder)
            => AddOtlpExporter(builder, name: null, configure: null);

        /// <summary>
        /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddOtlpExporter(this TracerProviderBuilder builder, Action<OtlpExporterOptions> configure)
            => AddOtlpExporter(builder, name: null, configure);

        /// <summary>
        /// Adds OpenTelemetry Protocol (OTLP) exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="TracerProviderBuilder"/> builder to use.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configure">Callback action for configuring <see cref="OtlpExporterOptions"/>.</param>
        /// <returns>The instance of <see cref="TracerProviderBuilder"/> to chain the calls.</returns>
        public static TracerProviderBuilder AddOtlpExporter(
            this TracerProviderBuilder builder,
            string name,
            Action<OtlpExporterOptions> configure)
        {
            Guard.ThrowIfNull(builder);

            name ??= Options.DefaultName;

            builder.ConfigureServices(services =>
            {
                if (configure != null)
                {
                    services.Configure(name, configure);
                }

                services.RegisterOptionsFactory(configuration => new SdkLimitOptions(configuration));
                services.RegisterOptionsFactory(configuration => new OtlpExporterOptions(configuration));
            });

            return builder.ConfigureBuilder((sp, builder) =>
            {
                var exporterOptions = sp.GetRequiredService<IOptionsMonitor<OtlpExporterOptions>>().Get(name);

                // Note: Not using name here for SdkLimitOptions. There should
                // only be one provider for a given service collection so
                // SdkLimitOptions is treated as a single default instance.
                var sdkLimitOptions = sp.GetRequiredService<IOptionsMonitor<SdkLimitOptions>>().CurrentValue;

                AddOtlpExporter(builder, exporterOptions, sdkLimitOptions, sp);
            });
        }

        internal static TracerProviderBuilder AddOtlpExporter(
            TracerProviderBuilder builder,
            OtlpExporterOptions exporterOptions,
            SdkLimitOptions sdkLimitOptions,
            IServiceProvider serviceProvider,
            Func<BaseExporter<Activity>, BaseExporter<Activity>> configureExporterInstance = null)
        {
            exporterOptions.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpTraceExporter");

            BaseExporter<Activity> otlpExporter = new OtlpTraceExporter(exporterOptions, sdkLimitOptions);

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
