// <copyright file="MetricPointsAccessor.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// A struct for accessing the <see cref="MetricPoint"/>s collected for a
    /// <see cref="Metric"/>.
    /// </summary>
    public readonly struct MetricPointsAccessor
    {
        private readonly MetricPoint[] metricsPoints;
        private readonly int[] metricPointsToProcess;
        private readonly long targetCount;
        private readonly DateTimeOffset start;
        private readonly DateTimeOffset end;

        internal MetricPointsAccessor(MetricPoint[] metricsPoints, int[] metricPointsToProcess, long targetCount, DateTimeOffset start, DateTimeOffset end)
        {
            Guard.Null(metricsPoints, nameof(metricsPoints));

            this.metricsPoints = metricsPoints;
            this.metricPointsToProcess = metricPointsToProcess;
            this.targetCount = targetCount;
            this.start = start;
            this.end = end;
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="MetricPointsAccessor"/>.
        /// </summary>
        /// <returns><see cref="Enumerator"/>.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this.metricsPoints, this.metricPointsToProcess, this.targetCount, this.start, this.end);
        }

        /// <summary>
        /// Enumerates the elements of a <see cref="MetricPointsAccessor"/>.
        /// </summary>
        public struct Enumerator
        {
            private readonly MetricPoint[] metricsPoints;
            private readonly int[] metricPointsToProcess;
            private readonly DateTimeOffset start;
            private readonly DateTimeOffset end;
            private readonly long targetCount;
            private long index;

            internal Enumerator(MetricPoint[] metricsPoints, int[] metricPointsToProcess, long targetCount, DateTimeOffset start, DateTimeOffset end)
            {
                this.metricsPoints = metricsPoints;
                this.metricPointsToProcess = metricPointsToProcess;
                this.targetCount = targetCount;
                this.index = -1;
                this.start = start;
                this.end = end;
            }

            /// <summary>
            /// Gets the <see cref="MetricPoint"/> at the current position of the enumerator.
            /// </summary>
            public ref readonly MetricPoint Current
            {
                get
                {
                    return ref this.metricsPoints[this.metricPointsToProcess[this.index]];
                }
            }

            /// <summary>
            /// Advances the enumerator to the next element of the <see
            /// cref="MetricPointsAccessor"/>.
            /// </summary>
            /// <returns><see langword="true"/> if the enumerator was
            /// successfully advanced to the next element; <see
            /// langword="false"/> if the enumerator has passed the end of the
            /// collection.</returns>
            public bool MoveNext()
            {
                while (++this.index < this.targetCount)
                {
                    ref var metricPoint = ref this.metricsPoints[this.metricPointsToProcess[this.index]];
                    metricPoint.StartTime = this.start;
                    metricPoint.EndTime = this.end;
                    return true;
                }

                return false;
            }
        }
    }
}
