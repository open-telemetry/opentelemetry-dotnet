// <copyright file="AggregateProcessor.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Threading;

#nullable enable

namespace OpenTelemetry.Metrics
{
    internal class AggregateProcessor : MeasurementProcessor
    {
        internal List<Aggregator> Aggregators { get; } = new List<Aggregator>();

        public override void OnEnd(MeasurementItem data)
        {
            data.State.Update(data.Point);
        }

        public void Register(Aggregator aggregator)
        {
            this.Aggregators.Add(aggregator);
        }

        public IEnumerable<Metric> Collect()
        {
            var metrics = new List<Metric>();

            foreach (var agg in this.Aggregators)
            {
                metrics.AddRange(agg.Collect());
            }

            return metrics.ToArray();
        }
    }
}
