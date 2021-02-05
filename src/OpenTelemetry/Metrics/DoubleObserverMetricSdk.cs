// <copyright file="DoubleObserverMetricSdk.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    internal class DoubleObserverMetricSdk : DoubleObserverMetric
    {
        private static readonly Func<LabelSet, DoubleObserverMetricHandleSdk> NewDoubleObserverMetricHandleSdkFunc = (_) => new DoubleObserverMetricHandleSdk();

        private readonly ConcurrentDictionary<LabelSet, DoubleObserverMetricHandleSdk> observerHandles = new ConcurrentDictionary<LabelSet, DoubleObserverMetricHandleSdk>();
        private readonly string metricName;
        private readonly Action<DoubleObserverMetric> callback;

        public DoubleObserverMetricSdk(string name, Action<DoubleObserverMetric> callback)
        {
            this.metricName = name;
            this.callback = callback;
        }

        public override void Observe(double value, LabelSet labelset)
        {
            // TODO cleanup of handle/aggregator.   Issue #530
            var boundInstrument = this.observerHandles.GetOrAdd(labelset, NewDoubleObserverMetricHandleSdkFunc);
            boundInstrument.Observe(value);
        }

        public override void Observe(double value, IEnumerable<KeyValuePair<string, string>> labels)
        {
            this.Observe(value, new LabelSetSdk(labels));
        }

        public void InvokeCallback()
        {
            this.callback(this);
        }

        internal ConcurrentDictionary<LabelSet, DoubleObserverMetricHandleSdk> GetAllHandles()
        {
            return this.observerHandles;
        }
    }
}
