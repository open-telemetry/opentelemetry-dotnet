// <copyright file="MeasureMetricSdk.cs" company="OpenTelemetry Authors">
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

using System.Collections.Concurrent;
using System.Collections.Generic;

namespace OpenTelemetry.Metrics
{
    internal abstract class MeasureMetricSdk<T> : MeasureMetric<T>
        where T : struct
    {
        private readonly ConcurrentDictionary<LabelSet, BoundMeasureMetricSdkBase<T>> measureBoundInstruments = new ConcurrentDictionary<LabelSet, BoundMeasureMetricSdkBase<T>>();
        private string metricName;

        public MeasureMetricSdk(string name)
        {
            this.metricName = name;
        }

        public override BoundMeasureMetric<T> Bind(LabelSet labelset)
        {
            return this.measureBoundInstruments.GetOrAdd(labelset, this.CreateMetric());
        }

        public override BoundMeasureMetric<T> Bind(IEnumerable<KeyValuePair<string, string>> labels)
        {
            return this.Bind(new LabelSetSdk(labels));
        }

        internal ConcurrentDictionary<LabelSet, BoundMeasureMetricSdkBase<T>> GetAllBoundInstruments()
        {
            return this.measureBoundInstruments;
        }

        protected abstract BoundMeasureMetricSdkBase<T> CreateMetric();
    }
}
