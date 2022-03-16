// <copyright file="InMemoryExporterMetricsExtensions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Threading;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods to simplify registering of the InMemory exporter.
    /// </summary>
    public static class InMemoryExporterMetricsExtensions
    {
        private const int DefaultExportIntervalMilliseconds = Timeout.Infinite;
        private const int DefaultExportTimeoutMilliseconds = Timeout.Infinite;

        /// <summary>
        /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/> using default options.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportedItems">Collection which will be populated with the exported MetricItem.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddInMemoryExporter(this MeterProviderBuilder builder, ICollection<Metric> exportedItems)
        {
            Guard.ThrowIfNull(builder);
            Guard.ThrowIfNull(exportedItems);

            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    AddInMemoryExporter(builder, exportedItems, sp.GetOptions<MetricReaderOptions>(), null);
                });
            }

            return AddInMemoryExporter(builder, exportedItems, new MetricReaderOptions(), null);
        }

        /// <summary>
        /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/> using default options.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportedItems">Collection which will be populated with the exported MetricItem.</param>
        /// <param name="configureMetricReader"><see cref="MetricReader"/> configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddInMemoryExporter(this MeterProviderBuilder builder, ICollection<Metric> exportedItems, Action<MetricReaderOptions> configureMetricReader)
        {
            Guard.ThrowIfNull(builder);
            Guard.ThrowIfNull(exportedItems);

            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    AddInMemoryExporter(builder, exportedItems, sp.GetOptions<MetricReaderOptions>(), configureMetricReader);
                });
            }

            return AddInMemoryExporter(builder, exportedItems, new MetricReaderOptions(), configureMetricReader);
        }

        private static MeterProviderBuilder AddInMemoryExporter(
            MeterProviderBuilder builder,
            ICollection<Metric> exportedItems,
            MetricReaderOptions metricReaderOptions,
            Action<MetricReaderOptions> configureMetricReader)
        {
            configureMetricReader?.Invoke(metricReaderOptions);

            var metricExporter = new InMemoryExporter<Metric>(exportedItems);

            var metricReader = PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
                metricExporter,
                metricReaderOptions,
                DefaultExportIntervalMilliseconds,
                DefaultExportTimeoutMilliseconds);

            return builder.AddReader(metricReader);
        }
    }
}
