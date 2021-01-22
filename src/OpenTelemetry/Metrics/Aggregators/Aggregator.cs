// <copyright file="Aggregator.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using OpenTelemetry.Metrics.Export;

namespace OpenTelemetry.Metrics.Aggregators
{
    /// <summary>
    /// Aggregator base class.
    /// </summary>
    /// <typeparam name="T">The type.</typeparam>
    [Obsolete("Metrics API/SDK is not recommended for production. See https://github.com/open-telemetry/opentelemetry-dotnet/issues/1501 for more information on metrics support.")]
    public abstract class Aggregator<T>
        where T : struct
    {
        private AggState active;
        private AggState checkpoint;

        protected Aggregator()
        {
            this.checkpoint = new AggState();
            this.active = new AggState();
        }

        /// <summary>
        /// Adds value to the running total in a thread safe manner.
        /// </summary>
        /// <param name="value">Value to be aggregated.</param>
        public virtual void Update(T value)
        {
            this.active.Increment();
        }

        /// <summary>
        /// Checkpoints the current aggregate data, and resets the state.
        /// </summary>
        public virtual void Checkpoint()
        {
            // checkpoints the start time for the current aggregation, and sets the new start time.
            this.checkpoint = Interlocked.Exchange(ref this.active, new AggState());
        }

        /// <summary>
        /// Check if checkpoint has any aggregated data.
        /// </summary>
        /// <returns>true if data was presented to aggregator.</returns>
        public virtual bool HasCheckpointData()
        {
            return this.checkpoint.Count > 0;
        }

        /// <summary>
        /// Convert checkpoint aggregator data to MetricData.
        /// </summary>
        /// <returns>An instance of <see cref="MetricData"/> representing the currently aggregated value.</returns>
        public abstract MetricData ToMetricData();

        /// <summary>
        /// Get Aggregation Type.
        /// </summary>
        /// <returns><see cref="AggregationType"/>.</returns>
        public abstract AggregationType GetAggregationType();

        /// <summary>
        /// Get the last interval end time before <see cref="Checkpoint"/> was called.
        /// </summary>
        /// <returns>The end timestamp of the last aggregated checkpoint.</returns>
        protected DateTimeOffset GetLastEndTimestamp()
        {
            return new DateTimeOffset(this.active.StartTimeTicks, TimeSpan.Zero).Subtract(TimeSpan.FromTicks(1));
        }

        /// <summary>
        /// Get the last interval start time before <see cref="Checkpoint"/> was called.
        /// </summary>
        /// <returns>The start timestamp of the last aggregated checkpoint.</returns>
        protected DateTimeOffset GetLastStartTimestamp()
        {
            return new DateTimeOffset(this.checkpoint.StartTimeTicks, TimeSpan.Zero);
        }

        private class AggState
        {
            private long count = 0;

            public AggState()
            {
                this.StartTimeTicks = DateTimeOffset.UtcNow.Ticks;
            }

            public long Count { get => this.count; }

            public long StartTimeTicks { get; }

            public void Increment()
            {
                Interlocked.Increment(ref this.count);
            }
        }
    }
}
