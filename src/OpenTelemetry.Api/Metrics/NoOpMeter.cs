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
            throw new NotImplementedException();
        }

        public override Gauge<double> CreateDoubleGauge(string name, bool monotonic = false)
        {
            throw new NotImplementedException();
        }

        public override Measure<double> CreateDoubleMeasure(string name, bool absolute = true)
        {
            throw new NotImplementedException();
        }

        public override Counter<long> CreateInt64Counter(string name, bool monotonic = true)
        {
            throw new NotImplementedException();
        }

        public override Gauge<long> CreateInt64Gauge(string name, bool monotonic = false)
        {
            throw new NotImplementedException();
        }

        public override Measure<long> CreateInt64Measure(string name, bool absolute = true)
        {
            throw new NotImplementedException();
        }

        public override LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels)
        {
            // return no op
            throw new System.NotImplementedException();
        }
    }
}
