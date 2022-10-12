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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Exporter.Prometheus;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods to simplify registering a PrometheusExporter.
    /// </summary>
    public static class PrometheusExporterMeterProviderBuilderExtensions
    {
        /// <summary>
        /// Adds <see cref="PrometheusExporter"/> to the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddPrometheusExporter(this MeterProviderBuilder builder)
            => AddPrometheusExporter(builder, name: null, configure: null);

        /// <summary>
        /// Adds <see cref="PrometheusExporter"/> to the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Callback action for configuring <see cref="PrometheusAspNetCoreOptions"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddPrometheusExporter(
            this MeterProviderBuilder builder,
            Action<PrometheusAspNetCoreOptions> configure)
            => AddPrometheusExporter(builder, name: null, configure);

        /// <summary>
        /// Adds <see cref="PrometheusExporter"/> to the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="name">Name which is used when retrieving options.</param>
        /// <param name="configure">Callback action for configuring <see cref="PrometheusAspNetCoreOptions"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddPrometheusExporter(
            this MeterProviderBuilder builder,
            string name,
            Action<PrometheusAspNetCoreOptions> configure)
        {
            Guard.ThrowIfNull(builder);

            name ??= Options.DefaultName;

            if (configure != null)
            {
                builder.ConfigureServices(services => services.Configure(name, configure));
            }

            return builder.ConfigureBuilder((sp, builder) =>
            {
                var options = sp.GetRequiredService<IOptionsMonitor<PrometheusAspNetCoreOptions>>().Get(name);

                AddPrometheusExporter(builder, options);
            });
        }

        private static MeterProviderBuilder AddPrometheusExporter(MeterProviderBuilder builder, PrometheusAspNetCoreOptions options)
        {
            var exporter = new PrometheusExporter(options.ExporterOptions);

            var reader = new BaseExportingMetricReader(exporter)
            {
                TemporalityPreference = MetricReaderTemporalityPreference.Cumulative,
            };

            return builder.AddReader(reader);
        }
    }
}
