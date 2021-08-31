// <copyright file="Metric.cs" company="OpenTelemetry Authors">
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;

namespace OpenTelemetry.Metrics
{
    public class Metric
    {
        private AggregatorStore aggStore;

        internal Metric(Instrument instrument, AggregationTemporality temporality)
        {
            this.Name = instrument.Name;
            this.Description = instrument.Description;
            this.Unit = instrument.Unit;
            this.Meter = instrument.Meter;
            AggregationType aggType = default;
            if (instrument.GetType() == typeof(ObservableCounter<long>)
                || instrument.GetType() == typeof(ObservableCounter<int>)
                || instrument.GetType() == typeof(ObservableCounter<short>)
                || instrument.GetType() == typeof(ObservableCounter<byte>))
            {
                aggType = AggregationType.LongSumIncomingCumulative;
                this.MetricType = MetricType.LongSum;
            }
            else if (instrument.GetType() == typeof(Counter<long>)
                || instrument.GetType() == typeof(Counter<int>)
                || instrument.GetType() == typeof(Counter<short>)
                || instrument.GetType() == typeof(Counter<byte>))
            {
                aggType = AggregationType.LongSumIncomingDelta;
                this.MetricType = MetricType.LongSum;
            }
            else if (instrument.GetType() == typeof(Counter<double>)
                || instrument.GetType() == typeof(Counter<float>))
            {
                aggType = AggregationType.DoubleSumIncomingDelta;
                this.MetricType = MetricType.DoubleSum;
            }
            else if (instrument.GetType() == typeof(ObservableCounter<double>)
                || instrument.GetType() == typeof(ObservableCounter<float>))
            {
                aggType = AggregationType.DoubleSumIncomingCumulative;
                this.MetricType = MetricType.DoubleSum;
            }

            this.aggStore = new AggregatorStore(aggType, temporality);
            this.Temporality = temporality;
        }

        public MetricType MetricType { get; private set; }

        public AggregationTemporality Temporality { get; private set; }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public string Unit { get; private set; }

        public Meter Meter { get; private set; }

        public BatchMetricPoint GetMetricPoints()
        {
            return this.aggStore.GetMetricPoints();
        }

        internal void UpdateLong(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            this.aggStore.UpdateLong(value, tags);
        }

        internal void UpdateDouble(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            this.aggStore.UpdateDouble(value, tags);
        }

        internal void SnapShot()
        {
            this.aggStore.SnapShot();
        }
    }
}
