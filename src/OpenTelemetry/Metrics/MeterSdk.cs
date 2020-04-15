// <copyright file="MeterSdk.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics
{
    // TODO: make MeterSdk internal
    public class MeterSdk : Meter
    {
        private readonly string meterName;
        private readonly MetricProcessor metricProcessor;
        private readonly IDictionary<string, Int64CounterMetricSdk> longCounters = new ConcurrentDictionary<string, Int64CounterMetricSdk>();
        private readonly IDictionary<string, DoubleCounterMetricSdk> doubleCounters = new ConcurrentDictionary<string, DoubleCounterMetricSdk>();
        private readonly IDictionary<string, Int64MeasureMetricSdk> longMeasures = new ConcurrentDictionary<string, Int64MeasureMetricSdk>();
        private readonly IDictionary<string, DoubleMeasureMetricSdk> doubleMeasures = new ConcurrentDictionary<string, DoubleMeasureMetricSdk>();
        private readonly IDictionary<string, Int64ObserverMetricSdk> longObservers = new ConcurrentDictionary<string, Int64ObserverMetricSdk>();
        private readonly IDictionary<string, DoubleObserverMetricSdk> doubleObservers = new ConcurrentDictionary<string, DoubleObserverMetricSdk>();
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

        public void Collect()
        {
            lock (this.collectLock)
            {
                // collect all pending metric updates and send to batcher.
                // must sync to prevent multiple Collect occuring at same time.
                var boundInstrumentsToRemove = new List<LabelSet>();
                foreach (var longCounter in this.longCounters)
                {
                    var metricName = longCounter.Key;
                    var counterInstrument = longCounter.Value;
                    foreach (var handle in counterInstrument.GetAllBoundInstruments())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        this.metricProcessor.Process(this.meterName, metricName, labelSet, aggregator);

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
                    foreach (var handle in counterInstrument.GetAllBoundInstruments())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        this.metricProcessor.Process(this.meterName, metricName, labelSet, aggregator);

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
                    foreach (var handle in measureInstrument.GetAllBoundInstruments())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        this.metricProcessor.Process(this.meterName, metricName, labelSet, aggregator);
                    }
                }

                foreach (var doubleMeasure in this.doubleMeasures)
                {
                    var metricName = doubleMeasure.Key;
                    var measureInstrument = doubleMeasure.Value;
                    foreach (var handle in measureInstrument.GetAllBoundInstruments())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        this.metricProcessor.Process(this.meterName, metricName, labelSet, aggregator);
                    }
                }

                foreach (var longObserver in this.longObservers)
                {
                    var metricName = longObserver.Key;
                    var observerInstrument = longObserver.Value;
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
                        this.metricProcessor.Process(this.meterName, metricName, labelSet, aggregator);
                    }
                }

                foreach (var doubleObserver in this.doubleObservers)
                {
                    var metricName = doubleObserver.Key;
                    var observerInstrument = doubleObserver.Value;
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
                        this.metricProcessor.Process(this.meterName, metricName, labelSet, aggregator);
                    }
                }
            }
        }

        public override CounterMetric<long> CreateInt64Counter(string name, bool monotonic = true)
        {
            if (!this.longCounters.TryGetValue(name, out var counter))
            {
                counter = new Int64CounterMetricSdk(name);

                this.longCounters.Add(name, counter);
            }

            return counter;
        }

        public override CounterMetric<double> CreateDoubleCounter(string name, bool monotonic = true)
        {
            if (!this.doubleCounters.TryGetValue(name, out var counter))
            {
                counter = new DoubleCounterMetricSdk(name);
                this.doubleCounters.Add(name, counter);
            }

            return counter;
        }

        public override MeasureMetric<double> CreateDoubleMeasure(string name, bool absolute = true)
        {
            if (!this.doubleMeasures.TryGetValue(name, out var measure))
            {
                measure = new DoubleMeasureMetricSdk(name);

                this.doubleMeasures.Add(name, measure);
            }

            return measure;
        }

        public override MeasureMetric<long> CreateInt64Measure(string name, bool absolute = true)
        {
            if (!this.longMeasures.TryGetValue(name, out var measure))
            {
                measure = new Int64MeasureMetricSdk(name);

                this.longMeasures.Add(name, measure);
            }

            return measure;
        }

        /// <inheritdoc/>
        public override Int64ObserverMetric CreateInt64Observer(string name, Action<Int64ObserverMetric> callback, bool absolute = true)
        {
            if (!this.longObservers.TryGetValue(name, out var observer))
            {
                observer = new Int64ObserverMetricSdk(name, callback);
                this.longObservers.Add(name, observer);
            }

            return observer;
        }

        /// <inheritdoc/>
        public override DoubleObserverMetric CreateDoubleObserver(string name, Action<DoubleObserverMetric> callback, bool absolute = true)
        {
            if (!this.doubleObservers.TryGetValue(name, out var observer))
            {
                observer = new DoubleObserverMetricSdk(name, callback);
                this.doubleObservers.Add(name, observer);
            }

            return observer;
        }
    }
}
