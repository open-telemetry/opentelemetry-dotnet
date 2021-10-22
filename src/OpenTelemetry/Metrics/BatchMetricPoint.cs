// <copyright file="BatchMetricPoint.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    public readonly struct BatchMetricPoint : IDisposable
    {
        private readonly MetricPoint[] metricsPoints;
        private readonly int[] metricPointsToProcess;
        private readonly long targetCount;
        private readonly DateTimeOffset start;
        private readonly DateTimeOffset end;

        internal BatchMetricPoint(MetricPoint[] metricsPoints, int[] metricPointsToProcess, long targetCount, DateTimeOffset start, DateTimeOffset end)
        {
            Guard.Null(metricsPoints, nameof(metricsPoints));

            this.metricsPoints = metricsPoints;
            this.metricPointsToProcess = metricPointsToProcess;
            this.targetCount = targetCount;
            this.start = start;
            this.end = end;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
        }

        /// <summary>
        /// Returns an enumerator that iterates through the <see cref="Batch{T}"/>.
        /// </summary>
        /// <returns><see cref="Enumerator"/>.</returns>
        public Enumerator GetEnumerator()
        {
            return new Enumerator(this.metricsPoints, this.metricPointsToProcess, this.targetCount, this.start, this.end);
        }

        /// <summary>
        /// Enumerates the elements of a <see cref="Batch{T}"/>.
        /// </summary>
        public struct Enumerator : IEnumerator
        {
            private readonly MetricPoint[] metricsPoints;
            private readonly int[] metricPointsToProcess;
            private readonly DateTimeOffset start;
            private readonly DateTimeOffset end;
            private long targetCount;
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

            public ref MetricPoint Current
            {
                get
                {
                    return ref this.metricsPoints[this.metricPointsToProcess[this.index]];
                }
            }

            /// <inheritdoc/>
            object IEnumerator.Current => this.Current;

            public void Dispose()
            {
            }

            /// <inheritdoc/>
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

            /// <inheritdoc/>
            public void Reset()
                => throw new NotSupportedException();
        }
    }
}
