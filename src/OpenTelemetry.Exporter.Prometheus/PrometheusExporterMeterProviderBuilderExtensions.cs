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

namespace OpenTelemetry.Metrics
{
    public static class PrometheusExporterMeterProviderBuilderExtensions
    {
        /// <summary>
        /// Adds <see cref="PrometheusExporter"/> to the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddPrometheusExporter(this MeterProviderBuilder builder, Action<PrometheusExporterOptions> configure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    AddPrometheusExporter(builder, sp.GetOptions<PrometheusExporterOptions>(), configure);
                });
            }

            return AddPrometheusExporter(builder, new PrometheusExporterOptions(), configure);
        }

        private static MeterProviderBuilder AddPrometheusExporter(MeterProviderBuilder builder, PrometheusExporterOptions options, Action<PrometheusExporterOptions> configure = null)
        {
            configure?.Invoke(options);

            var exporter = new PrometheusExporter(options);
            var reader = new BaseExportingMetricReader(exporter);

            return builder.AddReader(reader);
        }
    }
}
