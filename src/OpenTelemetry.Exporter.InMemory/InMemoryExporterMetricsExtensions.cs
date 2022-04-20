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
        /// <param name="exportedItems">Collection which will be populated with the exported MetricItem represented as <see cref="ExportableMetricCopy"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddInMemoryExporter(
            this MeterProviderBuilder builder,
            ICollection<ExportableMetricCopy> exportedItems)
        {
            return builder.AddInMemoryExporter(exportedItems: exportedItems, configureMetricReader: null);
        }

        /// <summary>
        /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/> using default options.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportFunc">A function which will replace <see cref="InMemoryExporter{T}.Export(in Batch{T})"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddInMemoryExporter(
            this MeterProviderBuilder builder,
            Func<Batch<Metric>, ExportResult> exportFunc)
        {
            Guard.ThrowIfNull(builder);
            Guard.ThrowIfNull(exportFunc);

            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    AddInMemoryExporter(builder, exportFunc, sp.GetOptions<MetricReaderOptions>(), null);
                });
            }

            return AddInMemoryExporter(builder, exportFunc, new MetricReaderOptions(), null);
        }

        /// <summary>
        /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/> using default options.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportedItems">Collection which will be populated with the exported MetricItem represented as <see cref="ExportableMetricCopy"/>.</param>
        /// <param name="configureMetricReader"><see cref="MetricReader"/> configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddInMemoryExporter(
            this MeterProviderBuilder builder,
            ICollection<ExportableMetricCopy> exportedItems,
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

        /// <summary>
        /// Adds InMemory metric exporter to the <see cref="MeterProviderBuilder"/> using default options.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportFunc">A function which will replace <see cref="InMemoryExporter{T}.Export(in Batch{T})"/>.</param>
        /// <param name="configureMetricReader"><see cref="MetricReader"/> configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddInMemoryExporter(
            this MeterProviderBuilder builder,
            Func<Batch<Metric>, ExportResult> exportFunc,
            Action<MetricReaderOptions> configureMetricReader)
        {
            Guard.ThrowIfNull(builder);
            Guard.ThrowIfNull(exportFunc);

            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    AddInMemoryExporter(builder, exportFunc, sp.GetOptions<MetricReaderOptions>(), configureMetricReader);
                });
            }

            return AddInMemoryExporter(builder, exportFunc, new MetricReaderOptions(), configureMetricReader);
        }

        private static MeterProviderBuilder AddInMemoryExporter(
            MeterProviderBuilder builder,
            ICollection<ExportableMetricCopy> exportedItems,
            MetricReaderOptions metricReaderOptions,
            Action<MetricReaderOptions> configureMetricReader)
        {
            configureMetricReader?.Invoke(metricReaderOptions);

            var metricExporter = new InMemoryExporter<Metric>(
                exportFunc: metricBatch =>
                {
                    foreach (var metric in metricBatch)
                    {
                        exportedItems.Add(new ExportableMetricCopy(metric));
                    }

                    return ExportResult.Success;
                });

            var metricReader = PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
                metricExporter,
                metricReaderOptions,
                DefaultExportIntervalMilliseconds,
                DefaultExportTimeoutMilliseconds);

            return builder.AddReader(metricReader);
        }

        private static MeterProviderBuilder AddInMemoryExporter(
            MeterProviderBuilder builder,
            Func<Batch<Metric>, ExportResult> exportFunc,
            MetricReaderOptions metricReaderOptions,
            Action<MetricReaderOptions> configureMetricReader)
        {
            configureMetricReader?.Invoke(metricReaderOptions);

            var metricExporter = new InMemoryExporter<Metric>(exportFunc);

            var metricReader = PeriodicExportingMetricReaderHelper.CreatePeriodicExportingMetricReader(
                metricExporter,
                metricReaderOptions,
                DefaultExportIntervalMilliseconds,
                DefaultExportTimeoutMilliseconds);

            return builder.AddReader(metricReader);
        }
    }
}
