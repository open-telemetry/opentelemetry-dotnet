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
using System.Collections.Generic;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics
{
    public class MeterSDK : Meter
    {
        private readonly MetricProcessor metricProcessor;

        internal MeterSDK(MetricProcessor metricProcessor)
        {
            this.metricProcessor = metricProcessor;
        }

        public override LabelSet GetLabelSet(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return new LabelSet(labels);
        }

        protected override Counter<T> CreateCounter<T>(string name, bool monotonic = true)
        {
            return new CounterSDK<T>(name, this.metricProcessor);
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
