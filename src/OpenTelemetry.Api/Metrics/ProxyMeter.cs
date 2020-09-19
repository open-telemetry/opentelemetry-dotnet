// <copyright file="ProxyMeter.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Threading;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Proxy Meter which act as a No-Op Meter, until real meter is provided.
    /// </summary>
    internal sealed class ProxyMeter : Meter
    {
        private Meter realMeter;

        public ProxyMeter()
        {
        }

        public override CounterMetric<double> CreateDoubleCounter(string name, bool monotonic = true)
        {
            return this.realMeter != null ? this.realMeter.CreateDoubleCounter(name, monotonic) : NoopCounterMetric<double>.Instance;
        }

        public override MeasureMetric<double> CreateDoubleMeasure(string name, bool absolute = true)
        {
            return this.realMeter != null ? this.realMeter.CreateDoubleMeasure(name, absolute) : NoopMeasureMetric<double>.Instance;
        }

        public override DoubleObserverMetric CreateDoubleObserver(string name, Action<DoubleObserverMetric> callback, bool absolute = true)
        {
            return this.realMeter != null ? this.realMeter.CreateDoubleObserver(name, callback, absolute) : NoopDoubleObserverMetric.Instance;
        }

        public override CounterMetric<long> CreateInt64Counter(string name, bool monotonic = true)
        {
            return this.realMeter != null ? this.realMeter.CreateInt64Counter(name, monotonic) : NoopCounterMetric<long>.Instance;
        }

        public override MeasureMetric<long> CreateInt64Measure(string name, bool absolute = true)
        {
            return this.realMeter != null ? this.realMeter.CreateInt64Measure(name, absolute) : NoopMeasureMetric<long>.Instance;
        }

        public override Int64ObserverMetric CreateInt64Observer(string name, Action<Int64ObserverMetric> callback, bool absolute = true)
        {
            return this.realMeter != null ? this.realMeter.CreateInt64Observer(name, callback, absolute) : NoopInt64ObserverMetric.Instance;
        }

        public override LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels)
        {
            // return no op
            return this.realMeter != null ? this.realMeter.GetLabelSet(labels) : LabelSet.BlankLabelSet;
        }

        public void UpdateMeter(Meter realMeter)
        {
            if (this.realMeter != null)
            {
                return;
            }

            // just in case user calls init concurrently
            Interlocked.CompareExchange(ref this.realMeter, realMeter, null);
        }
    }
}
