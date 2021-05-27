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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    internal class AggregateProcessor : MeasurementProcessor
    {
        internal ConcurrentDictionary<AggregatorStore, bool> AggregatorStores { get; } = new ConcurrentDictionary<AggregatorStore, bool>();

        internal override void OnEnd<T>(MeasurementItem measurementItem, ref DateTimeOffset dt, ref T value, ref ReadOnlySpan<KeyValuePair<string, object>> tags)
            where T : struct
        {
            measurementItem.State.Update(dt, value, tags);
        }

        internal void Register(AggregatorStore store)
        {
            this.AggregatorStores.TryAdd(store, true);
        }

        internal IEnumerable<Metric> Collect()
        {
            var metrics = new List<Metric>();

            foreach (var kv in this.AggregatorStores)
            {
                metrics.AddRange(kv.Key.Collect());
            }

            return metrics.ToArray();
        }
    }
}
