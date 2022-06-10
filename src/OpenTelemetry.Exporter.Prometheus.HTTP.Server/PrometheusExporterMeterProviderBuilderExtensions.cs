// <copyright file="PrometheusExporterMeterProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Exporter.Prometheus.HTTP.Server;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    public static class PrometheusExporterMeterProviderBuilderExtensions
    {
        /// <summary>
        /// Adds <see cref="PrometheusExporterHttpServer"/> to the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddMyPrometheusExporter(this MeterProviderBuilder builder, Action<PrometheusExporterHttpServerOptions> configure = null)
        {
            Guard.ThrowIfNull(builder);

            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    AddMyPrometheusExporter(builder, sp.GetOptions<PrometheusExporterHttpServerOptions>(), configure);
                });
            }

            return AddMyPrometheusExporter(builder, new PrometheusExporterHttpServerOptions(), configure);
        }

        private static MeterProviderBuilder AddMyPrometheusExporter(MeterProviderBuilder builder, PrometheusExporterHttpServerOptions options, Action<PrometheusExporterHttpServerOptions> configure = null)
        {
            configure?.Invoke(options);

            var exporter = new PrometheusExporterHttpServer(options);
            var reader = new BaseExportingMetricReader(exporter);
            reader.TemporalityPreference = MetricReaderTemporalityPreference.Cumulative;

            return builder.AddReader(reader);
        }
    }
}
