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
            // return no op
            throw new System.NotImplementedException();
        }

        public override Guage<double> CreateDoubleGauge(string name, bool monotonic = false)
        {
            // return no op
            throw new System.NotImplementedException();
        }

        public override Measure<double> CreateDoubleMeasure(string name, bool absolute = true)
        {
            throw new System.NotImplementedException();
        }

        public override Counter<long> CreateLongCounter(string name, bool monotonic = true)
        {
            // return no op
            throw new System.NotImplementedException();
        }

        public override Guage<long> CreateLongGauge(string name, bool monotonic = false)
        {
            // return no op
            throw new System.NotImplementedException();
        }

        public override Measure<long> CreateLongMeasure(string name, bool absolute = true)
        {
            throw new System.NotImplementedException();
        }

        public override LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels)
        {
            // return no op
            throw new System.NotImplementedException();
        }
    }
}
