// <copyright file="PrometheusExporter.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Exporter of Open Telemetry metrics to Prometheus.
    /// </summary>
    public class PrometheusExporter : MetricExporter
    {
        internal readonly PrometheusExporterOptions Options;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporter"/> class.
        /// </summary>
        /// <param name="options">Options for the exporter.</param>
        public PrometheusExporter(PrometheusExporterOptions options)
        {
            this.Options = options;
            this.Metrics = new List<Metric>();
        }

        private List<Metric> Metrics { get; set; }

        /// <inheritdoc/>
        public override Task<ExportResult> ExportAsync(IEnumerable<Metric> metrics, CancellationToken cancellationToken)
        {
            // Prometheus uses a pull model, not a push.
            // Accumulate the exported metrics internally, return success.
            // The pull process will read this internally stored metrics
            // at its own schedule.
            this.Metrics.AddRange(metrics);
            return Task.FromResult(ExportResult.Success);
        }

        internal List<Metric> GetAndClearMetrics()
        {
            // TODO harden this so as to not lose data if Export fails.
            List<Metric> current = this.Metrics;
            this.Metrics = new List<Metric>();
            return current;
        }
    }
}
