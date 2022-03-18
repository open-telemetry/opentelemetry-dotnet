// <copyright file="InMemoryMetricExporter.cs" company="OpenTelemetry Authors">
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

using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter
{
    internal class InMemoryMetricExporter : InMemoryExporter<Metric>
    {
        public InMemoryMetricExporter(ICollection<Metric> exportedItems)
            : base(exportedItems)
        {
        }

        public override ExportResult Export(in Batch<Metric> batch)
        {
            // By design, The MetricApi reuses Metrics (MetricReader.metricsCurrentBatch).
            // This means that exported Metrics will always reflect the latest values.
            // Here, we clear the exported collection to prevent populating
            // this with duplicate instances of the same Metric.
            this.ExportedItems.Clear();

            return base.Export(batch);
        }
    }
}
