﻿// <copyright file="UngroupedBatcher.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics.Aggregators;
using OpenTelemetry.Metrics.Implementation;

namespace OpenTelemetry.Metrics.Export
{
    /// <summary>
    /// Batcher which retains all dimensions/labels.
    /// </summary>
    public class UngroupedBatcher : MetricProcessor
    {
        private readonly MetricExporter exporter;        
        private readonly Task worker;
        private readonly TimeSpan aggregationInterval;
        private CancellationTokenSource cts;
        private List<Metric> metrics;

        /// <summary>
        /// Constructs UngroupedBatcher.
        /// </summary>
        /// <param name="exporter">Metric exporter instance.</param>
        /// <param name="aggregationInterval">Interval at which metrics are pushed to Exporter.</param>
        public UngroupedBatcher(MetricExporter exporter, TimeSpan aggregationInterval)
        {
            this.exporter = exporter ?? throw new ArgumentNullException(nameof(exporter));
            // TODO make this thread safe.
            this.metrics = new List<Metric>();
            this.aggregationInterval = aggregationInterval;
            this.cts = new CancellationTokenSource();
            this.worker = Task.Factory.StartNew(
                s => this.Worker((CancellationToken)s), this.cts.Token).ContinueWith((task) => Console.WriteLine("error"), TaskContinuationOptions.OnlyOnFaulted);
        }

        /// <summary>
        /// Constructs UngroupedBatcher.
        /// </summary>
        /// <param name="exporter">Metric exporter instance.</param>
        public UngroupedBatcher(MetricExporter exporter) : this(exporter, TimeSpan.FromSeconds(5))
        {
        }

        public override void ProcessCounter(string meterName, string metricName, LabelSet labelSet, CounterSumAggregator<long> sumAggregator)
        {
            var metric = new Metric(meterName, metricName, meterName + metricName, labelSet.Labels, sumAggregator.ValueFromLastCheckpoint());
            this.metrics.Add(metric);
        }

        public override void ProcessCounter(string meterName, string metricName, LabelSet labelSet, CounterSumAggregator<double> sumAggregator)
        {
            throw new NotImplementedException();
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

        private async Task Worker(CancellationToken cancellationToken)
        {
            try 
            {
                await Task.Delay(this.aggregationInterval, cancellationToken).ConfigureAwait(false);
                while (!cancellationToken.IsCancellationRequested)
                {
                    var sw = Stopwatch.StartNew();

                    if (this.metrics.Count > 0)
                    {
                        var metricToExport = this.metrics;
                        this.metrics = new List<Metric>();
                        await this.exporter.ExportAsync(metricToExport, cancellationToken);
                    }

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
            catch (Exception ex)
            {
                var s = ex.Message;
            }
        }
    }
}
