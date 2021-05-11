// <copyright file="InstrumentState.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;

#nullable enable

namespace OpenTelemetry.Metrics
{
    public class InstrumentState
    {
        private Instrument instrument;

        private IEnumerable<Action<DataPoint?>> aggregateUpdates;

        public InstrumentState(MeterProviderSdk sdk, Instrument instrument)
        {
            this.instrument = instrument;

            // Determine which aggregators we need

            var aggs = new List<Aggregator>()
            {
                new SumAggregator(instrument),
                new LastValueAggregator(instrument),
            };

            // Register with our AggregateProcessors

            var updates = new List<Action<DataPoint?>>();
            foreach (var agg in aggs)
            {
                updates.Add(agg.Update);

                foreach (var processor in sdk.AggregateProcessors)
                {
                    processor.Register(agg);
                }
            }

            this.aggregateUpdates = updates.ToArray();
        }

        public void Update(DataPoint? value)
        {
            // TODO: Find and filter based on tags

            foreach (var update in this.aggregateUpdates)
            {
                update.Invoke(value);
            }
        }
    }
}
