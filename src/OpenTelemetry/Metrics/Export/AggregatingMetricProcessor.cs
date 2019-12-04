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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Metrics.Implementation;

namespace OpenTelemetry.Metrics.Export
{
    public class AggregatingMetricProcessor : MetricProcessor
    {
        private readonly MetricExporter exporter;
        private readonly Task worker;
        private readonly TimeSpan aggregationInterval;
        private CancellationTokenSource cts;
        private IDictionary<string, Metric<long>> metricsLong = new ConcurrentDictionary<string, Metric<long>>();
        private IDictionary<string, Metric<double>> metricsDouble = new ConcurrentDictionary<string, Metric<double>>();

        /// <summary>
        /// Constructs aggregating processor.
        /// </summary>
        /// <param name="exporter">Metric exporter instance.</param>
        /// <param name="aggregationInterval">Interval at which metrics are aggregated.</param>
        public AggregatingMetricProcessor(MetricExporter exporter, TimeSpan aggregationInterval)
        {
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            this.aggregationInterval = aggregationInterval;
            this.cts = new CancellationTokenSource();
            this.worker = Task.Factory.StartNew(s => this.Worker((CancellationToken)s), this.cts.Token);
        }

        public override void ProcessCounter(string meterName, string metricName, LabelSet labelSet, CounterSumAggregator<long> sumAggregator)
        {
            if (!this.metricsLong.TryGetValue(metricName, out var metric))
            {
                this.metricsLong.Add(metricName, new Metric<long>(metricName));
            }

            var metricSeries = metric.GetOrCreateMetricTimeSeries(labelSet);
            metricSeries.Add(sumAggregator.Sum());
        }

        public override void ProcessCounter(string meterName, string metricName, LabelSet labelSet, CounterSumAggregator<double> sumAggregator)
        {
            if (!this.metricsDouble.TryGetValue(metricName, out var metric))
            {
                this.metricsDouble.Add(metricName, new Metric<double>(metricName));
            }

            var metricSeries = metric.GetOrCreateMetricTimeSeries(labelSet);
            metricSeries.Add(sumAggregator.Sum());
        }

        public override void ProcessGauge(string meterName, string metricName, LabelSet labelSet, GaugeAggregator<long> gaugeAggregator)
        {
            throw new NotImplementedException();
        }

        public override void ProcessGauge(string meterName, string metricName, LabelSet labelSet, GaugeAggregator<double> gaugeAggregator)
        {
            throw new NotImplementedException();
        }

        public override void ProcessMeasure(string meterName, string metricName, LabelSet labelSet, MeasureExactAggregator<long> measureAggregator)
        {
            throw new NotImplementedException();
        }

        public override void ProcessMeasure(string meterName, string metricName, LabelSet labelSet, MeasureExactAggregator<double> measureAggregator)
        {
            throw new NotImplementedException();
        }

        private async Task ExportBatchAsync(CancellationToken cancellationToken)
        {
            try
            {
                var metricLongs = new List<Metric<long>>();
                foreach (var keyValuePair in this.metricsLong)
                {
                    metricLongs.Add(keyValuePair.Value);
                }

                await this.exporter.ExportAsync(metricLongs, cancellationToken);
            }
            catch (Exception)
            {
            }
        }

        private async Task Worker(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var sw = Stopwatch.StartNew();
                await this.ExportBatchAsync(cancellationToken).ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                var remainingWait = this.aggregationInterval - sw.Elapsed;
                if (remainingWait > TimeSpan.Zero)
                {
                    await Task.Delay(remainingWait, cancellationToken).ConfigureAwait(false);
                }
            }
        }
    }
}
