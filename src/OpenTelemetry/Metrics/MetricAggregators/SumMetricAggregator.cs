// <copyright file="SumMetricAggregator.cs" company="OpenTelemetry Authors">
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
    internal class SumMetricAggregator : ISumMetric, IAggregator
    {
        private readonly object lockUpdate = new object();
        private long countLong = 0;
        private long countDouble = 0;
        private long sumLong = 0;
        private double sumDouble = 0;

        private DateTimeOffset lastStartTime;
        private long lastCountLong = 0;
        private long lastCountDouble = 0;
        private long lastSumLong = 0;
        private double lastSumDouble = 0;

        internal SumMetricAggregator(string name, string description, string unit, Meter meter, DateTimeOffset startTimeExclusive, KeyValuePair<string, object>[] attributes, bool isMonotonic, bool isDelta)
        {
            this.Name = name;
            this.Description = description;
            this.Unit = unit;
            this.Meter = meter;
            this.StartTimeExclusive = startTimeExclusive;
            this.Attributes = attributes;
            this.IsMonotonic = isMonotonic;
            this.IsDeltaTemporality = isDelta;

            this.lastStartTime = startTimeExclusive;
        }

        public string Name { get; private set; }

        public string Description { get; private set; }

        public string Unit { get; private set; }

        public Meter Meter { get; private set; }

        public DateTimeOffset StartTimeExclusive { get; private set; }

        public DateTimeOffset EndTimeInclusive { get; private set; }

        public KeyValuePair<string, object>[] Attributes { get; private set; }

        public bool IsDeltaTemporality { get; private set; }

        public bool IsMonotonic { get; }

        public IEnumerable<IExemplar> Exemplars { get; private set; } = new List<IExemplar>();

        public long Count => this.countLong + this.countDouble;

        public IDataValue Sum
        {
            get
            {
                if (this.countDouble == 0)
                {
                    return new DataValue(this.sumLong);
                }
                else
                {
                    return new DataValue(this.sumDouble + this.sumLong);
                }
            }
        }

        public void Update<T>(Instrument instrument, T value)
            where T : struct
        {
            bool isDeltaTemporality = true;

            Type genType = instrument.GetType().GetGenericTypeDefinition();
            if (genType == typeof(ObservableCounter<>) ||
                genType == typeof(ObservableGauge<>))
            {
                isDeltaTemporality = false;
            }

            if (typeof(T) == typeof(long))
            {
                var val = (long)(object)value;

                if (isDeltaTemporality)
                {
                    this.UpdateDeltaLong(val);
                }
                else
                {
                    this.UpdateCumulativeLong(val);
                }
            }
            else if (typeof(T) == typeof(double))
            {
                var val = (double)(object)value;

                if (isDeltaTemporality)
                {
                    this.UpdateDeltaDouble(val);
                }
                else
                {
                    this.UpdateCumulativeDouble(val);
                }
            }
            else
            {
                throw new Exception("Unsupported Type!");
            }
        }

        public IMetric Collect(DateTimeOffset dt, bool isDelta)
        {
            var cloneItem = new SumMetricAggregator(this.Name, this.Description, this.Unit, this.Meter, this.StartTimeExclusive, this.Attributes, this.IsMonotonic, this.IsDeltaTemporality);
            cloneItem.Exemplars = this.Exemplars;

            lock (this.lockUpdate)
            {
                if (this.IsDeltaTemporality == isDelta)
                {
                    if (this.IsDeltaTemporality)
                    {
                        cloneItem.StartTimeExclusive = this.lastStartTime;
                        cloneItem.EndTimeInclusive = dt;
                    }
                    else
                    {
                        cloneItem.StartTimeExclusive = this.StartTimeExclusive;
                        cloneItem.EndTimeInclusive = dt;
                    }

                    cloneItem.sumLong = this.sumLong;
                    cloneItem.sumDouble = this.sumDouble;

                    cloneItem.countLong = this.countLong;
                    cloneItem.countDouble = this.countDouble;
                }
                else
                {
                    // Need to convert Temporality!
                    if (this.IsDeltaTemporality && !isDelta)
                    {
                        // Convert DELTA -> CUM

                        cloneItem.IsDeltaTemporality = false;
                        cloneItem.StartTimeExclusive = this.StartTimeExclusive;
                        cloneItem.EndTimeInclusive = dt;

                        cloneItem.sumLong = this.lastSumLong + this.sumLong;
                        cloneItem.sumDouble = this.lastSumDouble + this.sumDouble;

                        cloneItem.countLong = this.lastCountLong + this.countLong;
                        cloneItem.countDouble = this.lastCountDouble + this.countDouble;
                    }
                    else if (!this.IsDeltaTemporality && isDelta)
                    {
                        // Convert CUM -> DELTA

                        cloneItem.IsDeltaTemporality = true;
                        cloneItem.StartTimeExclusive = this.lastStartTime;
                        cloneItem.EndTimeInclusive = dt;

                        cloneItem.sumLong = this.sumLong - this.lastSumLong;
                        cloneItem.sumDouble = this.sumDouble - this.lastSumDouble;

                        cloneItem.countLong = this.countLong - this.lastCountLong;
                        cloneItem.countDouble = this.countDouble - this.lastCountDouble;
                    }
                }

                // Update Last Sum/Count
                if (this.IsDeltaTemporality)
                {
                    this.lastStartTime = dt;
                    this.lastSumLong += this.sumLong;
                    this.lastSumDouble += this.sumDouble;
                    this.lastCountLong += this.countLong;
                    this.lastCountDouble += this.countDouble;

                    this.sumLong = 0;
                    this.sumDouble = 0;
                    this.countLong = 0;
                    this.countDouble = 0;
                }
                else
                {
                    this.lastStartTime = dt;
                    this.lastSumLong = this.sumLong;
                    this.lastSumDouble = this.sumDouble;
                    this.lastCountLong = this.countLong;
                    this.lastCountDouble = this.countDouble;
                }
            }

            return cloneItem;
        }

        public string ToDisplayString()
        {
            return $"Sum={this.Sum.Value}";
        }

        private void UpdateDeltaLong(long val)
        {
            lock (this.lockUpdate)
            {
                if (val < 0 && this.IsMonotonic)
                {
                    // TODO: log?
                    // Also, this validation can be done in earlier stage.
                }
                else
                {
                    this.sumLong += val;
                    this.countLong++;
                }
            }
        }

        private void UpdateDeltaDouble(double val)
        {
            lock (this.lockUpdate)
            {
                if (val < 0 && this.IsMonotonic)
                {
                    // TODO: log?
                    // Also, this validation can be done in earlier stage.
                }
                else
                {
                    this.sumDouble += val;
                    this.countDouble++;
                }
            }
        }

        private void UpdateCumulativeLong(long val)
        {
            lock (this.lockUpdate)
            {
                if (val < this.sumLong && this.countLong > 0 && this.IsMonotonic)
                {
                    // TODO: log?
                    // Also, this validation can be done in earlier stage.
                }
                else
                {
                    this.sumLong = val;
                    this.countLong++;
                }
            }
        }

        private void UpdateCumulativeDouble(double val)
        {
            lock (this.lockUpdate)
            {
                if (val < this.sumLong && this.countLong > 0 && this.IsMonotonic)
                {
                    // TODO: log?
                    // Also, this validation can be done in earlier stage.
                }
                else
                {
                    this.sumDouble = val;
                    this.countDouble++;
                }
            }
        }
    }
}
