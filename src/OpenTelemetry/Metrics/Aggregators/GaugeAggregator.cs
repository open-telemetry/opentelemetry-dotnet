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
using System.Threading;

namespace OpenTelemetry.Metrics.Aggregators
{
    /// <summary>
    /// Basic aggregator which keeps the last recorded value and timestamp.
    /// </summary>
    /// <typeparam name="T">Type of gauge.</typeparam>
    public class GaugeAggregator<T> : Aggregator<T>
        where T : struct
    {
        private GaugeData<T> current;
        private GaugeData<T> checkpoint;

        public GaugeAggregator()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        public override void Checkpoint()
        {
            this.checkpoint = Interlocked.Exchange<GaugeData<T>>(ref this.current, new GaugeData<T>());
        }

        public override void Update(T value)
        {
            var newState = new GaugeData<T>() { Value = value, Timestamp = DateTime.UtcNow };
            Interlocked.Exchange<GaugeData<T>>(ref this.current, newState);
        }

        public GaugeData<T> ValueFromLastCheckpoint()
        {
            return this.checkpoint;
        }
    }
}
