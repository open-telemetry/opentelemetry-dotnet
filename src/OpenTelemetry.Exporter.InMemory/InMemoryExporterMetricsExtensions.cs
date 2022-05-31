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
        /// <remarks>
        /// Be aware that <see cref="Metric"/> may continue to be updated after export.
        /// </remarks>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Metric"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddInMemoryExporter(this MeterProviderBuilder builder, ICollection<Metric> exportedItems)
        {
            return builder.AddInMemoryExporter(exportedItems: exportedItems, configureMetricReader: null);
        }

        /// <summary>
        /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <remarks>
        /// Be aware that <see cref="Metric"/> may continue to be updated after export.
        /// </remarks>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Metric"/>.</param>
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

        /// <summary>
        /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/> using default options.
        /// The exporter will be setup to export <see cref="MetricSnapshot"/>.
        /// </summary>
        /// <remarks>
        /// Use this if you need a copy of <see cref="Metric"/> that will not be updated after export.
        /// </remarks>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Metric"/> represented as <see cref="MetricSnapshot"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddInMemoryExporter(
            this MeterProviderBuilder builder,
            ICollection<MetricSnapshot> exportedItems)
        {
            return builder.AddInMemoryExporter(exportedItems: exportedItems, configureMetricReader: null);
        }

        /// <summary>
        /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/>.
        /// The exporter will be setup to export <see cref="MetricSnapshot"/>.
        /// </summary>
        /// <remarks>
        /// Use this if you need a copy of <see cref="Metric"/> that will not be updated after export.
        /// </remarks>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportedItems">Collection which will be populated with the exported <see cref="Metric"/> represented as <see cref="MetricSnapshot"/>.</param>
        /// <param name="configureMetricReader"><see cref="MetricReader"/> configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddInMemoryExporter(
            this MeterProviderBuilder builder,
            ICollection<MetricSnapshot> exportedItems,
            Action<MetricReaderOptions> configureMetricReader)
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

        private static MeterProviderBuilder AddInMemoryExporter(
            MeterProviderBuilder builder,
            ICollection<MetricSnapshot> exportedItems,
            MetricReaderOptions metricReaderOptions,
            Action<MetricReaderOptions> configureMetricReader)
        {
            configureMetricReader?.Invoke(metricReaderOptions);

            var metricExporter = new InMemoryExporter<Metric>(
                exportFunc: metricBatch => ExportMetricSnapshot(metricBatch, exportedItems));

            var metricReader = PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
                metricExporter,
                metricReaderOptions,
                DefaultExportIntervalMilliseconds,
                DefaultExportTimeoutMilliseconds);

            return builder.AddReader(metricReader);
        }

        private static ExportResult ExportMetricSnapshot(in Batch<Metric> batch, ICollection<MetricSnapshot> exportedItems)
        {
            if (exportedItems == null)
            {
                return ExportResult.Failure;
            }

            foreach (var metric in batch)
            {
                exportedItems.Add(new MetricSnapshot(metric));
            }

            return ExportResult.Success;
        }
    }
}
