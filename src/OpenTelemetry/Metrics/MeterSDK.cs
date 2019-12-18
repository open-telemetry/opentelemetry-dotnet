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
        private readonly IDictionary<string, CounterSdk<long>> longCounters = new ConcurrentDictionary<string, CounterSdk<long>>();
        private readonly IDictionary<string, CounterSdk<double>> doubleCounters = new ConcurrentDictionary<string, CounterSdk<double>>();
        private readonly IDictionary<string, GaugeSDK<long>> longGauges = new ConcurrentDictionary<string, GaugeSDK<long>>();
        private readonly IDictionary<string, GaugeSDK<double>> doubleGauges = new ConcurrentDictionary<string, GaugeSDK<double>>();
        private readonly IDictionary<string, MeasureSdk<long>> longMeasures = new ConcurrentDictionary<string, MeasureSdk<long>>();
        private readonly IDictionary<string, MeasureSdk<double>> doubleMeasures = new ConcurrentDictionary<string, MeasureSdk<double>>();
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
                    foreach (var handle in counterInstrument.GetAllHandles())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        this.metricProcessor.ProcessCounter(this.meterName, metricName, labelSet, aggregator);
                    }
                }

                foreach (var doubleCounter in this.doubleCounters)
                {
                    var metricName = doubleCounter.Key;
                    var counterInstrument = doubleCounter.Value;
                    foreach (var handle in counterInstrument.GetAllHandles())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        this.metricProcessor.ProcessCounter(this.meterName, metricName, labelSet, aggregator);
                    }
                }

                foreach (var longGauge in this.longGauges)
                {
                    var metricName = longGauge.Key;
                    var gaugeInstrument = longGauge.Value;
                    foreach (var handle in gaugeInstrument.GetAllHandles())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        this.metricProcessor.ProcessGauge(this.meterName, metricName, labelSet, aggregator);
                    }
                }

                foreach (var doubleGauge in this.doubleGauges)
                {
                    var metricName = doubleGauge.Key;
                    var gaugeInstrument = doubleGauge.Value;
                    foreach (var handle in gaugeInstrument.GetAllHandles())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        this.metricProcessor.ProcessGauge(this.meterName, metricName, labelSet, aggregator);
                    }
                }

                foreach (var longMeasure in this.longMeasures)
                {
                    var metricName = longMeasure.Key;
                    var measureInstrument = longMeasure.Value;
                    foreach (var handle in measureInstrument.GetAllHandles())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        this.metricProcessor.ProcessMeasure(this.meterName, metricName, labelSet, aggregator);
                    }
                }

                foreach (var doubleMeasure in this.doubleMeasures)
                {
                    var metricName = doubleMeasure.Key;
                    var measureInstrument = doubleMeasure.Value;
                    foreach (var handle in measureInstrument.GetAllHandles())
                    {
                        var labelSet = handle.Key;
                        var aggregator = handle.Value.GetAggregator();
                        aggregator.Checkpoint();
                        this.metricProcessor.ProcessMeasure(this.meterName, metricName, labelSet, aggregator);
                    }
                }
            }
        }

        public override Counter<long> CreateInt64Counter(string name, bool monotonic = true)
        {
            if (!this.longCounters.TryGetValue(name, out var counter))
            {
                counter = new CounterSdk<long>(name);

                this.longCounters.Add(name, counter);
            }

            return counter;
        }

        public override Counter<double> CreateDoubleCounter(string name, bool monotonic = true)
        {
            if (!this.doubleCounters.TryGetValue(name, out var counter))
            {
                counter = new CounterSdk<double>(name);
                this.doubleCounters.Add(name, counter);
            }

            return counter;
        }

        public override Gauge<long> CreateInt64Gauge(string name, bool monotonic = true)
        {
            if (!this.longGauges.TryGetValue(name, out var gauge))
            {
                gauge = new GaugeSDK<long>(name);

                this.longGauges.Add(name, gauge);
            }

            return gauge;
        }

        public override Gauge<double> CreateDoubleGauge(string name, bool monotonic = true)
        {
            if (!this.doubleGauges.TryGetValue(name, out var gauge))
            {
                gauge = new GaugeSDK<double>(name);

                this.doubleGauges.Add(name, gauge);
            }

            return gauge;
        }

        public override Measure<double> CreateDoubleMeasure(string name, bool absolute = true)
        {
            if (!this.doubleMeasures.TryGetValue(name, out var measure))
            {
                measure = new MeasureSdk<double>(name);

                this.doubleMeasures.Add(name, measure);
            }

            return measure;
        }

        public override Measure<long> CreateInt64Measure(string name, bool absolute = true)
        {
            if (!this.longMeasures.TryGetValue(name, out var measure))
            {
                measure = new MeasureSdk<long>(name);

                this.longMeasures.Add(name, measure);
            }

            return measure;
        }
    }
}
