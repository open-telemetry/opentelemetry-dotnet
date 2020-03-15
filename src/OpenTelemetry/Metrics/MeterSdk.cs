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

using System.Collections.Concurrent;
using System.Collections.Generic;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics
{
    // TODO: make MeterSdk internal
    public class MeterSdk : Meter
    {
        private readonly string meterName;
        private readonly MetricProcessor metricProcessor;
        private readonly IDictionary<string, CounterMetricSdk<long>> longCounters = new ConcurrentDictionary<string, CounterMetricSdk<long>>();
        private readonly IDictionary<string, CounterMetricSdk<double>> doubleCounters = new ConcurrentDictionary<string, CounterMetricSdk<double>>();
        private readonly IDictionary<string, MeasureMetricSdk<long>> longMeasures = new ConcurrentDictionary<string, MeasureMetricSdk<long>>();
        private readonly IDictionary<string, MeasureMetricSdk<double>> doubleMeasures = new ConcurrentDictionary<string, MeasureMetricSdk<double>>();
        private readonly IDictionary<string, ObserverMetricSdk<long>> longObservers = new ConcurrentDictionary<string, ObserverMetricSdk<long>>();
        private readonly IDictionary<string, ObserverMetricSdk<double>> doubleObservers = new ConcurrentDictionary<string, ObserverMetricSdk<double>>();
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
                    }
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
                    }
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

                foreach (var doubleObserver in this.doubleObservers)
                {
                    var metricName = doubleObserver.Key;
                    var measureInstrument = doubleObserver.Value;
                    foreach (var handle in measureInstrument.GetAllHandles())
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
                    var measureInstrument = longObserver.Value;
                    foreach (var handle in measureInstrument.GetAllHandles())
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
                counter = new CounterMetricSdk<long>(name);

                this.longCounters.Add(name, counter);
            }

            return counter;
        }

        public override CounterMetric<double> CreateDoubleCounter(string name, bool monotonic = true)
        {
            if (!this.doubleCounters.TryGetValue(name, out var counter))
            {
                counter = new CounterMetricSdk<double>(name);
                this.doubleCounters.Add(name, counter);
            }

            return counter;
        }

        public override MeasureMetric<double> CreateDoubleMeasure(string name, bool absolute = true)
        {
            if (!this.doubleMeasures.TryGetValue(name, out var measure))
            {
                measure = new MeasureMetricSdk<double>(name);

                this.doubleMeasures.Add(name, measure);
            }

            return measure;
        }

        public override MeasureMetric<long> CreateInt64Measure(string name, bool absolute = true)
        {
            if (!this.longMeasures.TryGetValue(name, out var measure))
            {
                measure = new MeasureMetricSdk<long>(name);

                this.longMeasures.Add(name, measure);
            }

            return measure;
        }

        /// <inheritdoc/>
        public override ObserverMetric<long> CreateInt64Observer(string name, bool absolute = true)
        {
            if (!this.longObservers.TryGetValue(name, out var observer))
            {
                observer = new ObserverMetricSdk<long>(name);

                this.longObservers.Add(name, observer);
            }

            return observer;
        }

        /// <inheritdoc/>
        public override ObserverMetric<double> CreateDoubleObserver(string name, bool absolute = true)
        {
            if (!this.doubleObservers.TryGetValue(name, out var observer))
            {
                observer = new ObserverMetricSdk<double>(name);

                this.doubleObservers.Add(name, observer);
            }

            return observer;
        }
    }
}
