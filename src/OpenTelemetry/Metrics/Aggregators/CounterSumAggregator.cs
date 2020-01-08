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

using System;
using System.Runtime.CompilerServices;
using System.Threading;

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

        public CounterSumAggregator()
        {
            if (typeof(T) != typeof(long) && typeof(T) != typeof(double))
            {
                throw new Exception("Invalid Type");
            }
        }

        public override void Checkpoint()
        {
            // checkpoints the current running sum into checkpoint, and starts counting again.
            if (typeof(T) == typeof(double))
            {
                this.checkPoint = (T)(object)Interlocked.Exchange(ref Unsafe.As<T, double>(ref this.sum), 0.0);
            }
            else
            {
                this.checkPoint = (T)(object)Interlocked.Exchange(ref Unsafe.As<T, long>(ref this.sum), 0);
            }
        }

        public override void Update(T value)
        {
            // Adds value to the running total in a thread safe manner.
            if (typeof(T) == typeof(double))
            {
                double initialTotal, computedTotal;
                do
                {
                    initialTotal = (double)(object)this.sum;
                    computedTotal = initialTotal + (double)(object)value;
                }
                while (initialTotal != Interlocked.CompareExchange(ref Unsafe.As<T, double>(ref this.sum), computedTotal, initialTotal));
            }
            else
            {
                Interlocked.Add(ref Unsafe.As<T, long>(ref this.sum), (long)(object)value);
            }
        }

        public T ValueFromLastCheckpoint()
        {
            return this.checkPoint;
        }
    }
}
