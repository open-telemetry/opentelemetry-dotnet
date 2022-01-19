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
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Contains extension methods for the <see cref="MeterProviderBuilder"/> class.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Adds a reader to the provider.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="reader"><see cref="MetricReader"/>.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProviderBuilder AddReader(this MeterProviderBuilder meterProviderBuilder, MetricReader reader)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                return meterProviderBuilderBase.AddReader(reader);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Add metric view, which can be used to customize the Metrics outputted
        /// from the SDK. The views are applied in the order they are added.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="instrumentName">Name of the instrument, to be used as part of Instrument selection criteria.</param>
        /// <param name="name">Name of the view. This will be used as name of resulting metrics stream.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        /// <remarks>See View specification here : https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view.</remarks>
        public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, string instrumentName, string name)
        {
            if (!MeterProviderBuilderSdk.IsValidInstrumentName(name))
            {
                throw new ArgumentException($"Custom view name {name} is invalid.", nameof(name));
            }

            if (instrumentName.IndexOf('*') != -1)
            {
                throw new ArgumentException(
                    $"Instrument selection criteria is invalid. Instrument name '{instrumentName}' " +
                    $"contains a wildcard character. This is not allowed when using a view to " +
                    $"rename a metric stream as it would lead to conflicting metric stream names.",
                    nameof(instrumentName));
            }

            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                return meterProviderBuilderBase.AddView(instrumentName, name);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Add metric view, which can be used to customize the Metrics outputted
        /// from the SDK. The views are applied in the order they are added.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="instrumentName">Name of the instrument, to be used as part of Instrument selection criteria.</param>
        /// <param name="metricStreamConfiguration">Aggregation configuration used to produce metrics stream.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        /// <remarks>See View specification here : https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view.</remarks>
        public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, string instrumentName, MetricStreamConfiguration metricStreamConfiguration)
        {
            if (metricStreamConfiguration == null)
            {
                throw new ArgumentNullException($"Metric stream configuration cannot be null.", nameof(metricStreamConfiguration));
            }

            if (!MeterProviderBuilderSdk.IsValidViewName(metricStreamConfiguration.Name))
            {
                throw new ArgumentException($"Custom view name {metricStreamConfiguration.Name} is invalid.", nameof(metricStreamConfiguration.Name));
            }

            if (metricStreamConfiguration.Name != null && instrumentName.IndexOf('*') != -1)
            {
                throw new ArgumentException(
                    $"Instrument selection criteria is invalid. Instrument name '{instrumentName}' " +
                    $"contains a wildcard character. This is not allowed when using a view to " +
                    $"rename a metric stream as it would lead to conflicting metric stream names.",
                    nameof(instrumentName));
            }

            if (metricStreamConfiguration is ExplicitBucketHistogramConfiguration histogramConfiguration)
            {
                // Validate histogram boundaries
                if (histogramConfiguration.Boundaries != null && !IsSortedAndDistinct(histogramConfiguration.Boundaries))
                {
                    throw new ArgumentException($"Histogram boundaries must be in ascending order with distinct values", nameof(histogramConfiguration.Boundaries));
                }
            }

            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                return meterProviderBuilderBase.AddView(instrumentName, metricStreamConfiguration);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Add metric view, which can be used to customize the Metrics outputted
        /// from the SDK. The views are applied in the order they are added.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="viewConfig">Function to configure aggregation based on the instrument.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        /// <remarks>See View specification here : https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view.</remarks>
        public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, Func<Instrument, MetricStreamConfiguration> viewConfig)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                return meterProviderBuilderBase.AddView(viewConfig);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Sets the maximum number of Metric streams supported by the MeterProvider.
        /// When no Views are configured, every instrument will result in one metric stream,
        /// so this control the numbers of instruments supported.
        /// When Views are configued, a single instrument can result in multiple metric streams,
        /// so this control the number of streams.
        /// </summary>
        /// <param name="meterProviderBuilder">MeterProviderBuilder instance.</param>
        /// <param name="maxMetricStreams">Maximum number of metric streams allowed.</param>
        /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
        /// <remarks>
        /// If an instrument is created, but disposed later, this will still be contributing to the limit.
        /// This may change in the future.
        /// </remarks>
        public static MeterProviderBuilder SetMaxMetricStreams(this MeterProviderBuilder meterProviderBuilder, int maxMetricStreams)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.SetMaxMetricStreams(maxMetricStreams);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Sets the maximum number of MetricPoints allowed per metric stream.
        /// This limits the number of unique combinations of key/value pairs used
        /// for reporting measurements.
        /// </summary>
        /// <param name="meterProviderBuilder">MeterProviderBuilder instance.</param>
        /// <param name="maxMetricPointsPerMetricStream">Maximum maximum number of metric points allowed per metric stream.</param>
        /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
        /// <remarks>
        /// If a particular key/value pair combination is used at least once,
        /// it will contribute to the limit for the life of the process.
        /// This may change in the future. See: https://github.com/open-telemetry/opentelemetry-dotnet/issues/2360.
        /// </remarks>
        public static MeterProviderBuilder SetMaxMetricPointsPerMetricStream(this MeterProviderBuilder meterProviderBuilder, int maxMetricPointsPerMetricStream)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.SetMaxMetricPointsPerMetricStream(maxMetricPointsPerMetricStream);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from. Overwrites currently set ResourceBuilder.
        /// </summary>
        /// <param name="meterProviderBuilder">MeterProviderBuilder instance.</param>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
        /// <returns>Returns <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder SetResourceBuilder(this MeterProviderBuilder meterProviderBuilder, ResourceBuilder resourceBuilder)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.SetResourceBuilder(resourceBuilder);
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
            if (meterProviderBuilder is IDeferredMeterProviderBuilder)
            {
                throw new NotSupportedException("DeferredMeterProviderBuilder requires a ServiceProvider to build.");
            }

            if (meterProviderBuilder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                return meterProviderBuilderSdk.BuildSdk();
            }

            return null;
        }

        private static bool IsSortedAndDistinct(double[] values)
        {
            for (int i = 1; i < values.Length; i++)
            {
                if (values[i] <= values[i - 1])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
