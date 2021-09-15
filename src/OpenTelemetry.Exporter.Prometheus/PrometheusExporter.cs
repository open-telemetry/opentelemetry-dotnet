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

using System;
using System.Collections.Generic;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter
{
    /// <summary>
    /// Exporter of OpenTelemetry metrics to Prometheus.
    /// </summary>
    [AggregationTemporality(AggregationTemporality.Cumulative)]
    [ExportModes(ExportModes.Pull)]
    public class PrometheusExporter : BaseExporter<Metric>
    {
        internal readonly PrometheusExporterOptions Options;
        internal Batch<Metric> Metrics;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrometheusExporter"/> class.
        /// </summary>
        /// <param name="options">Options for the exporter.</param>
        public PrometheusExporter(PrometheusExporterOptions options)
        {
            this.Options = options;
        }

        internal Action CollectMetric { get; set; }

        public override ExportResult Export(in Batch<Metric> metrics)
        {
            this.Metrics = metrics;
            return ExportResult.Success;
        }
    }
}
