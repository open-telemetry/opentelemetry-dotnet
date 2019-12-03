// <copyright file="MeterSDK.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics
{
    public class MeterSDK : Meter
    {
        private readonly MetricProcessor metricProcessor;
        private readonly IDictionary<string, CounterSDK<long>> longCounters = new ConcurrentDictionary<string, CounterSDK<long>>();
        private readonly IDictionary<string, CounterSDK<double>> doubleCounters = new ConcurrentDictionary<string, CounterSDK<double>>();

        internal MeterSDK(MetricProcessor metricProcessor)
        {
            this.metricProcessor = metricProcessor;
        }

        public override LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return new LabelSet(labels);
        }

        public void Collect()
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
                    var sumValue = handle.Value.GetSumAggregator();

                    this.metricProcessor.ProcessCounter(metricName, labelSet, sumValue);
                }
            }
        }

        public override Counter<long> CreateInt64Counter(string name, bool monotonic = true)
        {
            if (!this.longCounters.TryGetValue(name, out var counter))
            {
                counter = new CounterSDK<long>(name);

                this.longCounters.Add(name, counter);
            }

            return counter;
        }

        public override Counter<double> CreateDoubleCounter(string name, bool monotonic = true)
        {
            if (!this.doubleCounters.TryGetValue(name, out var counter))
            {
                counter = new CounterSDK<double>(name);
                this.doubleCounters.Add(name, counter);
            }

            return counter;
        }

        protected override Gauge<T> CreateGauge<T>(string name, bool monotonic = false)
        {
            throw new NotImplementedException();
        }

        protected override Measure<T> CreateMeasure<T>(string name, bool absolute = true)
        {
            throw new NotImplementedException();
        }
    }
}
