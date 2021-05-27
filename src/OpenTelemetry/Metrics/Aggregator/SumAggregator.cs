// <copyright file="SumAggregator.cs" company="OpenTelemetry Authors">
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
using System.Linq;

namespace OpenTelemetry.Metrics
{
    internal class SumAggregator : Aggregator
    {
        private readonly Instrument instrument;
        private readonly KeyValuePair<string, object>[] tags;
        private readonly object lockUpdate = new object();
        private Type valueType;
        private long sum = 0;
        private double dsum = 0;
        private long count = 0;

        internal SumAggregator(Instrument instrument, string[] names, object[] values)
        {
            this.instrument = instrument;

            if (names.Length != values.Length)
            {
                throw new ArgumentException("Length of names[] and values[] must match.");
            }

            this.tags = new KeyValuePair<string, object>[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                this.tags[i] = new KeyValuePair<string, object>(names[i], values[i]);
            }
        }

        internal override void Update<T>(DateTimeOffset dt, T value)
            where T : struct
        {
            lock (this.lockUpdate)
            {
                this.count++;
                this.valueType = typeof(T);

                // TODO: Need to handle DataPoint<T> appropriately

                if (typeof(T) == typeof(int))
                {
                    this.sum += (int)(object)value;
                }
                else if (typeof(T) == typeof(double))
                {
                    this.dsum += (double)(object)value;
                }
                else
                {
                    throw new Exception("Unsupported Type");
                }
            }
        }

        internal override IEnumerable<Metric> Collect()
        {
            // TODO: Need to determine how to convert to Metric

            if (this.count == 0)
            {
                return Enumerable.Empty<Metric>();
            }

            var dt = MeterProviderSdk.GetDateTimeOffset();

            IDataPoint datapointSum;
            IDataPoint datapointCount;
            lock (this.lockUpdate)
            {
                datapointCount = new DataPoint<int>(dt, (int)this.count, this.tags);

                if (this.valueType == typeof(int))
                {
                    datapointSum = new DataPoint<int>(dt, (int)this.sum, this.tags);
                    this.sum = 0;
                }
                else if (this.valueType == typeof(double))
                {
                    datapointSum = new DataPoint<double>(dt, (double)this.dsum, this.tags);
                    this.dsum = 0;
                }
                else
                {
                    throw new Exception("Unsupported Type");
                }

                this.count = 0;
            }

            var metrics = new Metric[]
            {
                new Metric($"{this.instrument.Meter.Name}:{this.instrument.Name}:Count", datapointCount),
                new Metric($"{this.instrument.Meter.Name}:{this.instrument.Name}:Sum", datapointSum),
            };

            return metrics;
        }
    }
}
