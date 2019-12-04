// <copyright file="AggregatingMetricProcessor.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics.Implementation;

namespace OpenTelemetry.Metrics.Export
{
    public class AggregatingMetricProcessor<T> : MetricProcessor<T> 
        where T : struct
    {
        private readonly MetricExporter<T> exporter;
        private Metric<T> metric;

        /// <summary>
        /// Constructs aggregating processor.
        /// </summary>
        /// <param name="metricName">Name of metric.</param>
        /// <param name="exporter">Metric exporter instance.</param>
        public AggregatingMetricProcessor(string metricName, MetricExporter<T> exporter)
        {
            this.metric = new Metric<T>(metricName);
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
        }

        public override void AddCounter(LabelSet labelSet, T value)
        {
            var metricSeries = this.metric.GetOrCreateMetricTimeSeries(labelSet);
            metricSeries.Add(value);

            // this.exporter.ExportAsync(counter, CancellationToken.None);
        }
    }
}
