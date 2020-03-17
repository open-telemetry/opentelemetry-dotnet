// <copyright file="CounterMetricSdk.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    internal class CounterMetricSdk<T> : CounterMetric<T>
        where T : struct
    {
        private readonly IDictionary<LabelSet, BoundCounterMetricSdk<T>> counterBoundInstruments = new ConcurrentDictionary<LabelSet, BoundCounterMetricSdk<T>>();
        private string metricName;

        public CounterMetricSdk()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        public CounterMetricSdk(string name) : this()
        {
            this.metricName = name;
        }

        public override BoundCounterMetric<T> Bind(LabelSet labelset)
        {
            if (!this.counterBoundInstruments.TryGetValue(labelset, out var boundInstrument))
            {
                boundInstrument = new BoundCounterMetricSdk<T>();

                this.counterBoundInstruments.Add(labelset, boundInstrument);
            }

            return boundInstrument;
        }

        public override BoundCounterMetric<T> Bind(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return this.Bind(new LabelSetSdk(labels));
        }

        internal IDictionary<LabelSet, BoundCounterMetricSdk<T>> GetAllBoundInstruments()
        {
            return this.counterBoundInstruments;
        }
    }
}
