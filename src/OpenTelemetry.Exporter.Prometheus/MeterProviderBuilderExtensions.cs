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
using OpenTelemetry.Exporter;

namespace OpenTelemetry.Metrics
{
    public static class MeterProviderBuilderExtensions
    {
        /// <summary>
        /// Adds Console exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The objects should not be disposed.")]
        public static MeterProviderBuilder AddPrometheusExporter(this MeterProviderBuilder builder, Action<PrometheusExporterOptions> configure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var options = new PrometheusExporterOptions();
            configure?.Invoke(options);

            var exporter = new PrometheusExporter(options);

            var metricReader = new BaseExportingMetricReader(exporter);
            exporter.CollectMetric = metricReader.Collect;

            var metricsHttpServer = new PrometheusExporterMetricsHttpServer(exporter);
            metricsHttpServer.Start();
            return builder.AddMetricReader(metricReader);
        }
    }
}
