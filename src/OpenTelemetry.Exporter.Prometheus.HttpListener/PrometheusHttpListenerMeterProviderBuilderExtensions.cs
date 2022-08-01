// <copyright file="PrometheusHttpListenerMeterProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods to simplify registering a PrometheusHttpListener.
    /// </summary>
    public static class PrometheusHttpListenerMeterProviderBuilderExtensions
    {
        /// <summary>
        /// Adds PrometheusHttpListener to MeterProviderBuilder.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/>builder to use.</param>
        /// <param name="configure">PrometheusHttpListenerOptions options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/>to chain calls.</returns>
        public static MeterProviderBuilder AddPrometheusHttpListener(
            this MeterProviderBuilder builder,
            Action<PrometheusHttpListenerOptions> configure = null)
        {
            Guard.ThrowIfNull(builder);

            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    AddPrometheusHttpListener(builder, sp.GetOptions<PrometheusHttpListenerOptions>(), configure);
                });
            }

            return AddPrometheusHttpListener(builder, new PrometheusHttpListenerOptions(), configure);
        }

        private static MeterProviderBuilder AddPrometheusHttpListener(
            MeterProviderBuilder builder,
            PrometheusHttpListenerOptions options,
            Action<PrometheusHttpListenerOptions> configure = null)
        {
            configure?.Invoke(options);

            var exporter = new PrometheusExporter(scrapeEndpointPath: options.ScrapeEndpointPath);

            var reader = new BaseExportingMetricReader(exporter)
            {
                TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
            };

            try
            {
                var listener = new PrometheusHttpListener(exporter, options);
                exporter.OnDispose = () => listener.Dispose();
                listener.Start();
            }
            catch (Exception ex)
            {
                try
                {
                    reader.Dispose();
                }
                catch
                {
                }

                throw new InvalidOperationException("PrometheusExporter HttpListener could not be started.", ex);
            }

            return builder.AddReader(reader);
        }
    }
}
