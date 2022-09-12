// <copyright file="ConsoleExporterMetricsExtensions.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods to simplify registering of the Console exporter.
    /// </summary>
    public static class ConsoleExporterMetricsExtensions
    {
        private const int DefaultExportIntervalMilliseconds = 10000;
        private const int DefaultExportTimeoutMilliseconds = Timeout.Infinite;

        /// <summary>
        /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/> using default options.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddConsoleExporter(this MeterProviderBuilder builder)
        {
            return AddConsoleExporter(builder, configureExporter: null);
        }

        /// <summary>
        /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="configureExporter">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddConsoleExporter(this MeterProviderBuilder builder, Action<ConsoleExporterOptions> configureExporter)
        {
            Guard.ThrowIfNull(builder);

            if (configureExporter != null)
            {
                builder.ConfigureServices(services => services.Configure(configureExporter));
            }

            return builder.ConfigureBuilder((sp, builder) =>
            {
                AddConsoleExporter(
                    builder,
                    sp.GetRequiredService<IOptions<ConsoleExporterOptions>>().Value,
                    sp.GetRequiredService<IOptions<MetricReaderOptions>>().Value);
            });
        }

        /// <summary>
        /// Adds <see cref="ConsoleMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="configureExporterAndMetricReader">Exporter and <see cref="MetricReader"/> configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddConsoleExporter(
            this MeterProviderBuilder builder,
            Action<ConsoleExporterOptions, MetricReaderOptions> configureExporterAndMetricReader)
        {
            Guard.ThrowIfNull(builder);

            return builder.ConfigureBuilder((sp, builder) =>
            {
                var exporterOptions = sp.GetRequiredService<IOptions<ConsoleExporterOptions>>().Value;
                var metricReaderOptions = sp.GetRequiredService<IOptions<MetricReaderOptions>>().Value;

                configureExporterAndMetricReader?.Invoke(exporterOptions, metricReaderOptions);

                AddConsoleExporter(builder, exporterOptions, metricReaderOptions);
            });
        }

        private static MeterProviderBuilder AddConsoleExporter(
            MeterProviderBuilder builder,
            ConsoleExporterOptions exporterOptions,
            MetricReaderOptions metricReaderOptions)
        {
            var metricExporter = new ConsoleMetricExporter(exporterOptions);

            var metricReader = PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
                metricExporter,
                metricReaderOptions,
                DefaultExportIntervalMilliseconds,
                DefaultExportTimeoutMilliseconds);

            return builder.AddReader(metricReader);
        }
    }
}
