// <copyright file="PrometheusExporter.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Metrics.Implementation;

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

        internal List<Metric> Metrics { get; private set; }

        /// <inheritdoc/>
        public override Task<ExportResult> ExportAsync(List<Metric> metrics, CancellationToken cancellationToken)
        {
            // Prometheus uses a pull process, not a push
            // Store the updated metrics internally for the next pull and return success.
            this.Metrics = metrics;
            return Task.FromResult(ExportResult.Success);
        }
    }
}
