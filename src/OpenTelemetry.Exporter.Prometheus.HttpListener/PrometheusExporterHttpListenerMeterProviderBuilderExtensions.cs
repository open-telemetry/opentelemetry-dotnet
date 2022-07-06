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
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.Prometheus.HttpListener;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    public static class PrometheusExporterHttpListenerMeterProviderBuilderExtensions
    {
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
            var exporter = new PrometheusExporter(exporterOptions);
            var reader = new BaseExportingMetricReader(exporter)
            {
                TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
            };

            configureListenerOptions?.Invoke(listenerOptions);
            var listener = new PrometheusHttpListener(exporter, listenerOptions);

            const string HttpListenerStartFailureExceptionMessage = "PrometheusExporter http listener could not be started.";
            try
            {
                listener.Start();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(HttpListenerStartFailureExceptionMessage, ex);
            }

            return builder.AddReader(reader);
        }
    }
}
