// <copyright file="CounterSumAggregator.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics.Aggregators
{
    /// <summary>
    /// Basic aggregator which calculates a Sum from individual measurements.
    /// </summary>
    /// <typeparam name="T">Type of counter.</typeparam>
    public class CounterSumAggregator<T> : Aggregator<T>
        where T : struct
    {
        private T sum;
        private T checkPoint;

        public override void Checkpoint()
        {
            this.checkPoint = this.sum;
        }

        public override void Update(T value)
        {
            // TODO discuss if we should move away from generics to avoid
            // these conversions.
            if (typeof(T) == typeof(double))
            {
                this.sum = (T)(object)((double)(object)this.sum + (double)(object)value);
            }
            else
            {
                this.sum = (T)(object)((long)(object)this.sum + (long)(object)value);
            }
        }

        public T ValueFromLastCheckpoint()
        {
            return this.checkPoint;
        }
    }
}
