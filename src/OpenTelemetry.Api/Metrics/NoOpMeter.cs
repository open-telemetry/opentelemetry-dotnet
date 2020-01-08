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

        public override Counter<double> CreateDoubleCounter(string name, bool monotonic = true)
        {
            return NoOpCounter<double>.Instance;
        }

        public override Gauge<double> CreateDoubleGauge(string name, bool monotonic = false)
        {
            return NoOpGauge<double>.Instance;
        }

        public override Measure<double> CreateDoubleMeasure(string name, bool absolute = true)
        {
            return NoOpMeasure<double>.Instance;
        }

        public override Counter<long> CreateInt64Counter(string name, bool monotonic = true)
        {
            return NoOpCounter<long>.Instance;
        }

        public override Gauge<long> CreateInt64Gauge(string name, bool monotonic = false)
        {
            return NoOpGauge<long>.Instance;
        }

        public override Measure<long> CreateInt64Measure(string name, bool absolute = true)
        {
            return NoOpMeasure<long>.Instance;
        }

        public override LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels)
        {
            // return no op
            return LabelSet.BlankLabelSet;
        }
    }
}
