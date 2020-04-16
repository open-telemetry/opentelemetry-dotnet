// <copyright file="Int64ObserverMetricSdk.cs" company="OpenTelemetry Authors">
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
    internal class Int64ObserverMetricSdk : Int64ObserverMetric
    {
        private readonly IDictionary<LabelSet, Int64ObserverMetricHandleSdk> observerHandles = new ConcurrentDictionary<LabelSet, Int64ObserverMetricHandleSdk>();
        private readonly string metricName;
        private readonly Action<Int64ObserverMetric> callback;

        public Int64ObserverMetricSdk(string name, Action<Int64ObserverMetric> callback)
        {
            this.metricName = name;
            this.callback = callback;
        }

        public override void Observe(long value, LabelSet labelset)
        {
            if (!this.observerHandles.TryGetValue(labelset, out var boundInstrument))
            {
                boundInstrument = new Int64ObserverMetricHandleSdk();

                // TODO cleanup of handle/aggregator.   Issue #530
                this.observerHandles.Add(labelset, boundInstrument);
            }

            boundInstrument.Observe(value);
        }

        public override void Observe(long value, IEnumerable<KeyValuePair<string, string>> labels)
        {
            this.Observe(value, new LabelSetSdk(labels));
        }

        public void InvokeCallback()
        {
            this.callback(this);
        }

        internal IDictionary<LabelSet, Int64ObserverMetricHandleSdk> GetAllHandles()
        {
            return this.observerHandles;
        }
    }
}
