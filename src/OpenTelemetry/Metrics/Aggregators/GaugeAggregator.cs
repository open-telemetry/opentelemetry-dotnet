// <copyright file="GaugeAggregator.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics.Aggregators
{
    /// <summary>
    /// Basic aggregator which keeps the last recorded value and timestamp.
    /// </summary>
    /// <typeparam name="T">Type of gauge.</typeparam>
    public class GaugeAggregator<T> : Aggregator<T>
        where T : struct
    {
        private T value;
        private DateTime timestamp;
        private Tuple<T, DateTime> checkpoint;

        public override void Checkpoint()
        {
            this.checkpoint = new Tuple<T, DateTime>(this.value, this.timestamp);
        }

        public override void Update(T value)
        {
            if (typeof(T) == typeof(double))
            {
                this.value = (T)(object)((double)(object)value);
            }
            else
            {
                this.value = (T)(object)((long)(object)value);
            }

            this.timestamp = DateTime.UtcNow;
        }

        public Tuple<T, DateTime> ValueFromLastCheckpoint()
        {
            return this.checkpoint;
        }
    }
}
