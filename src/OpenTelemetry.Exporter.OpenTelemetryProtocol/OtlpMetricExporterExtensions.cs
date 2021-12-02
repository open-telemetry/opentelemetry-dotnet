// <copyright file="OtlpMetricExporterExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Extension methods to simplify registering of the OpenTelemetry Protocol (OTLP) exporter.
    /// </summary>
    public static class OtlpMetricExporterExtensions
    {
        /// <summary>
        /// Adds <see cref="OtlpMetricExporter"/> to the <see cref="MeterProviderBuilder"/>.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <param name="optionsBuilder"><see cref="OltpMetricExporterOptionsBuilder"/>.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddOtlpExporter(
            this MeterProviderBuilder builder,
            Action<OtlpExporterOptions> configure = null,
            OltpMetricExporterOptionsBuilder optionsBuilder = null)
        {
            Guard.Null(builder, nameof(builder));

            optionsBuilder ??= new();

            if (configure != null)
            {
                optionsBuilder.Configure(configure);
            }

            if (builder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
            {
                return deferredMeterProviderBuilder.Configure((sp, builder) =>
                {
                    AddOtlpExporter(builder, optionsBuilder, sp);
                });
            }

            return AddOtlpExporter(builder, optionsBuilder, serviceProvider: null);
        }

        private static MeterProviderBuilder AddOtlpExporter(
            MeterProviderBuilder builder,
            OltpMetricExporterOptionsBuilder optionsBuilder,
            IServiceProvider serviceProvider)
        {
            var initialEndpoint = optionsBuilder.BuilderOptions.Endpoint;

            var options = optionsBuilder.Build(serviceProvider);

            options.TryEnableIHttpClientFactoryIntegration(serviceProvider, "OtlpMetricExporter");

            options.AppendExportPath(initialEndpoint, OtlpExporterOptions.MetricsExportPath);

            var metricExporter = new OtlpMetricExporter(options);

            var metricReader = options.MetricReaderType == MetricReaderType.Manual
                ? new BaseExportingMetricReader(metricExporter)
                : new PeriodicExportingMetricReader(metricExporter, options.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds);

            metricReader.Temporality = options.AggregationTemporality;
            return builder.AddReader(metricReader);
        }
    }
}
