// <copyright file="InMemoryExporterMetricsExtensions.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    public static class InMemoryExporterMetricsExtensions
    {
        /// <summary>
        /// Adds InMemory exporter to the TracerProvider.
        /// </summary>
        /// <param name="builder"><see cref="MeterProviderBuilder"/> builder to use.</param>
        /// <param name="exportedItems">Collection which will be populated with the exported MetricItem.</param>
        /// <returns>The instance of <see cref="MeterProviderBuilder"/> to chain the calls.</returns>
        public static MeterProviderBuilder AddInMemoryExporter(this MeterProviderBuilder builder, ICollection<Metric> exportedItems)
        {
            Guard.ThrowIfNull(builder, nameof(builder));
            Guard.ThrowIfNull(exportedItems, nameof(exportedItems));

            return builder.AddReader(new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems)));
        }
    }
}
