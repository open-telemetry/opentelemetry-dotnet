// <copyright file="PrometheusExporterHttpListenerMeterProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Prometheus.HttpListener;
using OpenTelemetry.Exporter.Prometheus.Shared;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods to simplify registering a PrometheusHttpListener.
    /// </summary>
    public static class PrometheusExporterHttpListenerMeterProviderBuilderExtensions
    {
        /// <summary>
        /// Adds Prometheus exporter to MeterProviderBuilder.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/>builder to use.</param>
        /// <param name="configureExporterOptions">Exporter configuration options.</param>
        /// <param name="configureListenerOptions">HttpListener options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/>to chain calls.</returns>
        public static MeterProviderBuilder AddPrometheusHttpListener(
            this MeterProviderBuilder builder,
            Action<PrometheusExporterOptions> configureExporterOptions = null,
            Action<PrometheusHttpListenerOptions> configureListenerOptions = null)
        {
            Guard.ThrowIfNull(builder);

            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    AddPrometheusHttpListener(
                        builder,
                        sp.GetOptions<PrometheusExporterOptions>(),
                        sp.GetOptions<PrometheusHttpListenerOptions>(),
                        configureExporterOptions,
                        configureListenerOptions);
                });
            }

            return AddPrometheusHttpListener(
                builder,
                new PrometheusExporterOptions(),
                new PrometheusHttpListenerOptions(),
                configureExporterOptions,
                configureListenerOptions);
        }

        private static MeterProviderBuilder AddPrometheusHttpListener(
            MeterProviderBuilder builder,
            PrometheusExporterOptions exporterOptions,
            PrometheusHttpListenerOptions listenerOptions,
            Action<PrometheusExporterOptions> configureExporterOptions = null,
            Action<PrometheusHttpListenerOptions> configureListenerOptions = null)
        {
            configureExporterOptions?.Invoke(exporterOptions);
            configureListenerOptions?.Invoke(listenerOptions);

            var exporter = new PrometheusExporter(exporterOptions);

            var reader = new BaseExportingMetricReader(exporter)
            {
                TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
            };

            const string HttpListenerStartFailureExceptionMessage = "PrometheusExporter HttpListener could not be started.";
            try
            {
                var listener = new PrometheusHttpListener(exporter, listenerOptions);
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

                throw new InvalidOperationException(HttpListenerStartFailureExceptionMessage, ex);
            }

            return builder.AddReader(reader);
        }
    }
}
