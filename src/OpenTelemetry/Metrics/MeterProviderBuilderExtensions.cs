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

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Contains extension methods for the <see cref="MeterProviderBuilder"/> class.
    /// </summary>
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Sets observation period.
        /// </summary>
        /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
        /// <param name="periodMilliseconds">Perion in milliseconds.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProviderBuilder SetObservationPeriod(this MeterProviderBuilder meterProviderBuilder, int periodMilliseconds)
        {
            if (meterProviderBuilder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                return meterProviderBuilderSdk.SetObservationPeriod(periodMilliseconds);
            }

            return meterProviderBuilder;
        }

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
        /// <param name="periodMilliseconds">Perion in milliseconds.</param>
        /// <returns><see cref="MeterProvider"/>.</returns>
        public static MeterProviderBuilder AddExportProcessor(this MeterProviderBuilder meterProviderBuilder, MetricProcessor processor, int periodMilliseconds)
        {
            if (meterProviderBuilder is MeterProviderBuilderSdk meterProviderBuilderSdk)
            {
                return meterProviderBuilderSdk.AddExporter(processor, periodMilliseconds);
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
