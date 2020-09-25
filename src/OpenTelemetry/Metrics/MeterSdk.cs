// <copyright file="MeterSdk.cs" company="OpenTelemetry Authors">
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics
{
    internal class MeterSdk : Meter
    {
        private readonly string meterName;
        private readonly MetricProcessor metricProcessor;
        private readonly ConcurrentDictionary<string, Int64CounterMetricSdk> longCounters = new ConcurrentDictionary<string, Int64CounterMetricSdk>();
        private readonly ConcurrentDictionary<string, DoubleCounterMetricSdk> doubleCounters = new ConcurrentDictionary<string, DoubleCounterMetricSdk>();
        private readonly ConcurrentDictionary<string, Int64MeasureMetricSdk> longMeasures = new ConcurrentDictionary<string, Int64MeasureMetricSdk>();
        private readonly ConcurrentDictionary<string, DoubleMeasureMetricSdk> doubleMeasures = new ConcurrentDictionary<string, DoubleMeasureMetricSdk>();
        private readonly ConcurrentDictionary<string, Int64ObserverMetricSdk> longObservers = new ConcurrentDictionary<string, Int64ObserverMetricSdk>();
        private readonly ConcurrentDictionary<string, DoubleObserverMetricSdk> doubleObservers = new ConcurrentDictionary<string, DoubleObserverMetricSdk>();
        private readonly object collectLock = new object();

        internal MeterSdk(string meterName, MetricProcessor metricProcessor)
        {
            this.meterName = meterName;
            this.metricProcessor = metricProcessor;
        }

        public override LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return new LabelSetSdk(labels);
        }

        public virtual void Collect()
        {
            lock (this.collectLock)
            {
                OpenTelemetrySdkEventSource.Log.MeterCollectInvoked(this.meterName);

                // collect all pending metric updates and send to batcher.
                // must sync to prevent multiple Collect occurring at same time.
                var boundInstrumentsToRemove = new List<LabelSet>();
                foreach (var longCounter in this.longCounters)
                {
                    var metricName = longCounter.Key;
                    var counterInstrument = longCounter.Value;
                    var metric = new Metric(this.meterName, metricName, this.meterName + metricName, AggregationType.LongSum);
                    foreach (var handle in counterInstrument.GetAllBoundInstruments())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        var metricData = aggregator.ToMetricData();
                        metricData.Labels = labelSet.Labels;
                        metric.Data.Add(metricData);

                        // Updates so far are pushed to Processor/Exporter.
                        // Adjust status accordinly.
                        // The status flows from initial UpdatePending, to
                        // NoPendingUpdate, to CandidateForRemoval, to physical removal.
                        // i.e UpdatePending->NoPendingUpdate->CandidateForRemoval->removal
                        if (handle.Value.Status == RecordStatus.CandidateForRemoval)
                        {
                            // The actual removal doesn't occur here as we are still
                            // iterating the dictionary.
                            boundInstrumentsToRemove.Add(labelSet);
                        }
                        else if (handle.Value.Status == RecordStatus.UpdatePending)
                        {
                            handle.Value.Status = RecordStatus.NoPendingUpdate;
                        }
                        else if (handle.Value.Status == RecordStatus.NoPendingUpdate)
                        {
                            handle.Value.Status = RecordStatus.CandidateForRemoval;
                        }
                    }

                    this.metricProcessor.Process(metric);
                    foreach (var boundInstrumentToRemove in boundInstrumentsToRemove)
                    {
                        // This actual unbinding or removal of the record occurs inside UnBind
                        // which synchronizes with Bind to ensure no record with pending update
                        // is lost.
                        counterInstrument.UnBind(boundInstrumentToRemove);
                    }

                    boundInstrumentsToRemove.Clear();
                }

                foreach (var doubleCounter in this.doubleCounters)
                {
                    var metricName = doubleCounter.Key;
                    var counterInstrument = doubleCounter.Value;
                    var metric = new Metric(this.meterName, metricName, this.meterName + metricName, AggregationType.DoubleSum);
                    foreach (var handle in counterInstrument.GetAllBoundInstruments())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        var metricData = aggregator.ToMetricData();
                        metricData.Labels = labelSet.Labels;
                        metric.Data.Add(metricData);

                        // Updates so far are pushed to Processor/Exporter.
                        // Adjust status accordinly.
                        // The status flows from initial UpdatePending, to
                        // NoPendingUpdate, to CandidateForRemoval, to physical removal.
                        // i.e UpdatePending->NoPendingUpdate->CandidateForRemoval->removal
                        if (handle.Value.Status == RecordStatus.CandidateForRemoval)
                        {
                            // The actual removal doesn't occur here as we are still
                            // iterating the dictionary.
                            boundInstrumentsToRemove.Add(labelSet);
                        }
                        else if (handle.Value.Status == RecordStatus.UpdatePending)
                        {
                            handle.Value.Status = RecordStatus.NoPendingUpdate;
                        }
                        else if (handle.Value.Status == RecordStatus.NoPendingUpdate)
                        {
                            handle.Value.Status = RecordStatus.CandidateForRemoval;
                        }
                    }

                    this.metricProcessor.Process(metric);
                    foreach (var boundInstrumentToRemove in boundInstrumentsToRemove)
                    {
                        // This actual unbinding or removal of the record occurs inside UnBind
                        // which synchronizes with Bind to ensure no record with pending update
                        // is lost.
                        counterInstrument.UnBind(boundInstrumentToRemove);
                    }

                    boundInstrumentsToRemove.Clear();
                }

                foreach (var longMeasure in this.longMeasures)
                {
                    var metricName = longMeasure.Key;
                    var measureInstrument = longMeasure.Value;
                    var metric = new Metric(this.meterName, metricName, this.meterName + metricName, AggregationType.Int64Summary);
                    foreach (var handle in measureInstrument.GetAllBoundInstruments())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        var metricData = aggregator.ToMetricData();
                        metricData.Labels = labelSet.Labels;
                        metric.Data.Add(metricData);
                    }

                    this.metricProcessor.Process(metric);
                }

                foreach (var doubleMeasure in this.doubleMeasures)
                {
                    var metricName = doubleMeasure.Key;
                    var measureInstrument = doubleMeasure.Value;
                    var metric = new Metric(this.meterName, metricName, this.meterName + metricName, AggregationType.DoubleSummary);
                    foreach (var handle in measureInstrument.GetAllBoundInstruments())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        var metricData = aggregator.ToMetricData();
                        metricData.Labels = labelSet.Labels;
                        metric.Data.Add(metricData);
                    }

                    this.metricProcessor.Process(metric);
                }

                foreach (var longObserver in this.longObservers)
                {
                    var metricName = longObserver.Key;
                    var observerInstrument = longObserver.Value;
                    var metric = new Metric(this.meterName, metricName, this.meterName + metricName, AggregationType.LongSum);
                    try
                    {
                        // TODO: Decide if we want to enforce a timeout. Issue # 542
                        observerInstrument.InvokeCallback();
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricObserverCallbackException(metricName, ex);
                    }

                    foreach (var handle in observerInstrument.GetAllHandles())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        var metricData = aggregator.ToMetricData();
                        metricData.Labels = labelSet.Labels;
                        metric.Data.Add(metricData);
                    }

                    this.metricProcessor.Process(metric);
                }

                foreach (var doubleObserver in this.doubleObservers)
                {
                    var metricName = doubleObserver.Key;
                    var observerInstrument = doubleObserver.Value;
                    var metric = new Metric(this.meterName, metricName, this.meterName + metricName, AggregationType.LongSum);
                    try
                    {
                        // TODO: Decide if we want to enforce a timeout. Issue # 542
                        observerInstrument.InvokeCallback();
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetrySdkEventSource.Log.MetricObserverCallbackException(metricName, ex);
                    }

                    foreach (var handle in observerInstrument.GetAllHandles())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        var metricData = aggregator.ToMetricData();
                        metricData.Labels = labelSet.Labels;
                        metric.Data.Add(metricData);
                    }

                    this.metricProcessor.Process(metric);
                }
            }
        }

        public override CounterMetric<long> CreateInt64Counter(string name, bool monotonic = true)
        {
            return this.longCounters.GetOrAdd(name, new Int64CounterMetricSdk(name));
        }

        public override CounterMetric<double> CreateDoubleCounter(string name, bool monotonic = true)
        {
            return this.doubleCounters.GetOrAdd(name, new DoubleCounterMetricSdk(name));
        }

        public override MeasureMetric<double> CreateDoubleMeasure(string name, bool absolute = true)
        {
            return this.doubleMeasures.GetOrAdd(name, new DoubleMeasureMetricSdk(name));
        }

        public override MeasureMetric<long> CreateInt64Measure(string name, bool absolute = true)
        {
            return this.longMeasures.GetOrAdd(name, new Int64MeasureMetricSdk(name));
        }

        /// <inheritdoc/>
        public override Int64ObserverMetric CreateInt64Observer(string name, Action<Int64ObserverMetric> callback, bool absolute = true)
        {
            return this.longObservers.GetOrAdd(name, new Int64ObserverMetricSdk(name, callback));
        }

        /// <inheritdoc/>
        public override DoubleObserverMetric CreateDoubleObserver(string name, Action<DoubleObserverMetric> callback, bool absolute = true)
        {
            return this.doubleObservers.GetOrAdd(name, new DoubleObserverMetricSdk(name, callback));
        }
    }
}
