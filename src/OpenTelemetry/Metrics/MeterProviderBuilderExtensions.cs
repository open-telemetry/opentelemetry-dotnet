// <copyright file="MeterProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Contains extension methods for the <see cref="MeterProviderBuilder"/> class.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Sets default collection period.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="periodMilliseconds">Perion in milliseconds.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProviderBuilder SetDefaultCollectionPeriod(this MeterProviderBuilder meterProviderBuilder, int periodMilliseconds)
        {
            if (meterProviderBuilder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                return meterProviderBuilderSdk.SetDefaultCollectionPeriod(periodMilliseconds);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Add measurement processor.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="processor">Measurement Processors.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProviderBuilder AddProcessor(this MeterProviderBuilder meterProviderBuilder, MeasurementProcessor processor)
        {
            if (meterProviderBuilder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                return meterProviderBuilderSdk.AddMeasurementProcessor(processor);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Add export processor.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="processor">Measurement Processors.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProviderBuilder AddExportProcessor(this MeterProviderBuilder meterProviderBuilder, MetricProcessor processor)
        {
            if (meterProviderBuilder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                return meterProviderBuilderSdk.AddExporter(processor);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Add export processor.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="processor">Measurement Processors.</param>
        /// <param name="collectionPeriodMilliseconds">Period in milliseconds between Collections.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProviderBuilder AddExportProcessor(this MeterProviderBuilder meterProviderBuilder, MetricProcessor processor, int collectionPeriodMilliseconds)
        {
            if (meterProviderBuilder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                return meterProviderBuilderSdk.AddExporter(processor, collectionPeriodMilliseconds);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Add view configuration.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="meterName">Meter name.</param>
        /// <param name="meterVersion">Meter version.</param>
        /// <param name="instrumentName">Instrument name.</param>
        /// <param name="instrumentKind">Instrument version.</param>
        /// <param name="aggregator">Aggregator to use.</param>
        /// <param name="aggregatorParam">Aggregator parameter (Optional).</param>
        /// <param name="viewName">View name (Optional).</param>
        /// <param name="viewDescription">View description (Optional).</param>
        /// <param name="attributeKeys">Attribute keys to include (Optional).</param>
        /// <param name="extraDimensions">Attribute keys from baggage/context to include (Optional).</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProviderBuilder AddView(
            this MeterProviderBuilder meterProviderBuilder,
            string meterName = null,
            string meterVersion = null,
            string instrumentName = null,
            string instrumentKind = null,
            Aggregator aggregator = Aggregator.SUMMARY,
            object aggregatorParam = null,
            string viewName = null,
            string viewDescription = null,
            string[] attributeKeys = null,
            string[] extraDimensions = null)
        {
            if (meterProviderBuilder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                return meterProviderBuilderSdk.AddView(
                    meterName,
                    meterVersion,
                    instrumentName,
                    instrumentKind,
                    aggregator,
                    aggregatorParam,
                    viewName,
                    viewDescription,
                    attributeKeys,
                    extraDimensions);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Run the given actions to initialize the <see cref="MeterProvider"/>.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProvider Build(this MeterProviderBuilder meterProviderBuilder)
        {
            if (meterProviderBuilder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                return meterProviderBuilderSdk.Build();
            }

            return null;
        }
    }
}
