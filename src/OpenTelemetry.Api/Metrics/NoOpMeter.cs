// <copyright file="NoOpMeter.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    internal sealed class NoOpMeter : Meter
    {
        public NoOpMeter()
        {
        }

        public override CounterMetric<double> CreateDoubleCounter(string name, bool monotonic = true)
        {
            return NoOpCounterMetric<double>.Instance;
        }

        public override MeasureMetric<double> CreateDoubleMeasure(string name, bool absolute = true)
        {
            return NoOpMeasureMetric<double>.Instance;
        }

        public override ObserverMetric<double> CreateDoubleObserver(string name, bool monotonic = true)
        {
            return NoOpObserverMetric<double>.Instance;
        }

        public override CounterMetric<long> CreateInt64Counter(string name, bool monotonic = true)
        {
            return NoOpCounterMetric<long>.Instance;
        }

        public override MeasureMetric<long> CreateInt64Measure(string name, bool absolute = true)
        {
            return NoOpMeasureMetric<long>.Instance;
        }

        public override ObserverMetric<long> CreateInt64Observer(string name, bool absolute = true)
        {
            return NoOpObserverMetric<long>.Instance;
        }

        public override LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels)
        {
            // return no op
            return LabelSet.BlankLabelSet;
        }
    }
}
