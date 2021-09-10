// <copyright file="InMemoryExporterMetricHelperExtensions.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using OpenTelemetry.Exporter;

namespace OpenTelemetry.Metrics
{
    public static class InMemoryExporterMetricHelperExtensions
    {
        /// <summary>
        /// Adds InMemory exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportedItems">Collection which will be populated with the exported MetricItem.</param>
        /// <param name="configure">Exporter configuration options.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "The objects should not be disposed.")]
        public static MeterProviderBuilder AddInMemoryExporter(this MeterProviderBuilder builder, ICollection<Metric> exportedItems, Action<InMemoryExporterOptions> configure = null)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            if (exportedItems == null)
            {
                throw new ArgumentNullException(nameof(exportedItems));
            }

            var options = new InMemoryExporterOptions();
            configure?.Invoke(options);

            var exporter = new InMemoryMetricExporter(exportedItems, options);
            return builder.AddMetricReader(new PeriodicExportingMetricReader(exporter, options.MetricExportIntervalMilliseconds));
        }
    }
}
