// <copyright file="GaugeMetricAggregator.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    internal class GaugeMetricAggregator : IGaugeMetric, IAggregator
    {
        private readonly object lockUpdate = new object();
        private IDataValue value;

        internal GaugeMetricAggregator(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes)
        {
            this.Name = name;
            this.Description = description;
            this.Unit = unit;
            this.Meter = meter;
            this.StartTimeExclusive = startTimeExclusive;
            this.Attributes = attributes;

            // TODO: Split this class into two or leverage generic
            this.MetricType = MetricType.LongGauge;
        }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public string Unit { get; private set; }

        public Meter Meter { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; private set; }

        public DateTimeOffset EndTimeInclusive { get; private set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public IEnumerable<IExemplar> Exemplars { get; private set; } = new List<IExemplar>();

        public IDataValue LastValue => this.value;

        public MetricType MetricType { get; private set; }

        public void Update<T>(T value)
            where T : struct
        {
            lock (this.lockUpdate)
            {
                this.value = new DataValue<T>(value);
            }
        }

        public IMetric Collect(DateTimeOffset dt, bool isDelta)
        {
            var cloneItem = new GaugeMetricAggregator(this.Name, this.Description, this.Unit, this.Meter, this.StartTimeExclusive, this.Attributes);

            lock (this.lockUpdate)
            {
                cloneItem.Exemplars = this.Exemplars;
                cloneItem.EndTimeInclusive = dt;
                cloneItem.value = this.LastValue;
            }

            return cloneItem;
        }

        public string ToDisplayString()
        {
            return $"Last={this.LastValue.Value}";
        }
    }
}
