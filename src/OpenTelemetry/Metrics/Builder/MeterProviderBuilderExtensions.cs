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

#nullable enable

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Contains extension methods for the <see cref="MeterProviderBuilder"/> class.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Adds instrumentation to the provider.
        /// </summary>
        /// <remarks>
        /// Note: The type specified by <typeparamref name="T"/> will be
        /// registered as a singleton service into application services.
        /// </remarks>
        /// <typeparam name="T">Instrumentation type.</typeparam>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder AddInstrumentation<T>(this MeterProviderBuilder meterProviderBuilder)
            where T : class
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.AddInstrumentation<T>();
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Adds a reader to the provider.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="reader"><see cref="MetricReader"/>.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder AddReader(this MeterProviderBuilder meterProviderBuilder, MetricReader reader)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.AddReader(reader);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Adds a reader to the provider.
        /// </summary>
        /// <remarks>
        /// Note: The type specified by <typeparamref name="T"/> will be
        /// registered as a singleton service into application services.
        /// </remarks>
        /// <typeparam name="T">Reader type.</typeparam>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder AddReader<T>(this MeterProviderBuilder meterProviderBuilder)
            where T : MetricReader
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.AddReader<T>();
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Add metric view, which can be used to customize the Metrics outputted
        /// from the SDK. The views are applied in the order they are added.
        /// </summary>
        /// <remarks>See View specification here : https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view.</remarks>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="instrumentName">Name of the instrument, to be used as part of Instrument selection criteria.</param>
        /// <param name="name">Name of the view. This will be used as name of resulting metrics stream.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, string instrumentName, string name)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.AddView(instrumentName, name);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Add metric view, which can be used to customize the Metrics outputted
        /// from the SDK. The views are applied in the order they are added.
        /// </summary>
        /// <remarks>See View specification here : https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view.</remarks>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="instrumentName">Name of the instrument, to be used as part of Instrument selection criteria.</param>
        /// <param name="metricStreamConfiguration">Aggregation configuration used to produce metrics stream.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, string instrumentName, MetricStreamConfiguration metricStreamConfiguration)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.AddView(instrumentName, metricStreamConfiguration);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Add metric view, which can be used to customize the Metrics outputted
        /// from the SDK. The views are applied in the order they are added.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>Note: An invalid <see cref="MetricStreamConfiguration"/>
        /// returned from <paramref name="viewConfig"/> will cause the
        /// view to be ignored, no error will be
        /// thrown at runtime.</item>
        /// <item>See View specification here : https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#view.</item>
        /// </list>
        /// </remarks>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="viewConfig">Function to configure aggregation based on the instrument.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder AddView(this MeterProviderBuilder meterProviderBuilder, Func<Instrument, MetricStreamConfiguration?> viewConfig)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.AddView(viewConfig);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Sets the maximum number of Metric streams supported by the MeterProvider.
        /// When no Views are configured, every instrument will result in one metric stream,
        /// so this control the numbers of instruments supported.
        /// When Views are configured, a single instrument can result in multiple metric streams,
        /// so this control the number of streams.
        /// </summary>
        /// <remarks>
        /// If an instrument is created, but disposed later, this will still be contributing to the limit.
        /// This may change in the future.
        /// </remarks>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="maxMetricStreams">Maximum number of metric streams allowed.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
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
        /// <remarks>
        /// If a particular key/value pair combination is used at least once,
        /// it will contribute to the limit for the life of the process.
        /// This may change in the future. See: https://github.com/open-telemetry/opentelemetry-dotnet/issues/2360.
        /// </remarks>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="maxMetricPointsPerMetricStream">Maximum maximum number of metric points allowed per metric stream.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
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
        /// You should usually use <see cref="ConfigureResource(MeterProviderBuilder, Action{ResourceBuilder})"/> instead
        /// (call <see cref="ResourceBuilder.Clear"/> if desired).
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder SetResourceBuilder(this MeterProviderBuilder meterProviderBuilder, ResourceBuilder resourceBuilder)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.SetResourceBuilder(resourceBuilder);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Modify the <see cref="ResourceBuilder"/> from which the Resource associated with
        /// this provider is built from in-place.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="configure">An action which modifies the provided <see cref="ResourceBuilder"/> in-place.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder ConfigureResource(this MeterProviderBuilder meterProviderBuilder, Action<ResourceBuilder> configure)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.ConfigureResource(configure);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="IServiceCollection"/> where metric services are configured.
        /// </summary>
        /// <remarks>
        /// Note: Metric services are only available during the application
        /// configuration phase.
        /// </remarks>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder ConfigureServices(
            this MeterProviderBuilder meterProviderBuilder,
            Action<IServiceCollection> configure)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                meterProviderBuilderBase.ConfigureServices(configure);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Register a callback action to configure the <see
        /// cref="MeterProviderBuilder"/> once the application <see
        /// cref="IServiceProvider"/> is available.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="configure">Configuration callback.</param>
        /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
        public static MeterProviderBuilder ConfigureBuilder(
            this MeterProviderBuilder meterProviderBuilder,
            Action<IServiceProvider, MeterProviderBuilder> configure)
        {
            if (meterProviderBuilder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                deferredMeterProviderBuilder.Configure(configure);
            }

            return meterProviderBuilder;
        }

        /// <summary>
        /// Run the given actions to initialize the <see cref="MeterProvider"/>.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProvider? Build(this MeterProviderBuilder meterProviderBuilder)
        {
            if (meterProviderBuilder is MeterProviderBuilderBase meterProviderBuilderBase)
            {
                return meterProviderBuilderBase.InvokeBuild();
            }

            return null;
        }
    }
}
