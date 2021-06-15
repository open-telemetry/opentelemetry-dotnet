// <copyright file="Timer.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Metrics
{
    /// <summary>
    /// Timer instrument.
    /// </summary>
    /// <typeparam name="T">Support <c>int</c>, <c>long</c>, <c>double</c>.</typeparam>
    public sealed class Timer<T> : Instrument<T>
        where T : struct
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Timer{T}"/> class.
        /// </summary>
        /// <param name="meter"><see cref="Meter"/>.</param>
        /// <param name="name">Name.</param>
        /// <param name="description">Description.</param>
        public Timer(Meter meter, string name, string description)
            : base(meter, name, "ms", description)
        {
            this.Publish();
        }

        /// <summary>
        /// Start a timer for a Timer instrument.
        /// </summary>
        /// <param name="tags">Attributes.</param>
        /// <returns>Timer (<see cref="TimeMarkerWithTag{T}"/>) state.</returns>
        public TimeMarkerWithTag<T> Start(params KeyValuePair<string, object>[] tags)
        {
            return new TimeMarkerWithTag<T>(this, tags);
        }

        /// <summary>
        /// Stop and record time for a Timer instrument.
        /// </summary>
        /// <param name="mark">Timer state.</param>
        public void Stop(TimeMarkerWithTag<T> mark)
        {
            if (mark.TimerMark.Timer == this)
            {
                mark.Dispose();
            }
            else
            {
                throw new Exception("Mismatched Timer!");
            }
        }

        /// <summary>
        /// Start a timer for a Timer instrument.
        /// </summary>
        /// <returns>Timer (<see cref="TimeMarker{T}"/>) state.</returns>
        public TimeMarker<T> Start()
        {
            return new TimeMarker<T>(this);
        }

        /// <summary>
        /// Stop and record time for a Timer instrument.
        /// </summary>
        /// <param name="mark">Timer state.</param>
        /// <param name="tags">Attributes.</param>
        public void Stop(TimeMarker<T> mark, params KeyValuePair<string, object>[] tags)
        {
            if (mark.Timer == this)
            {
                this.RecordMeasurement(mark.ElapsedMilliseconds, tags);
            }
            else
            {
                throw new Exception("Mismatched Timer!");
            }
        }

        /// <summary>
        /// TimeMarker records the start state of a Timer instrument.
        /// </summary>
        /// <typeparam name="T1">Support <c>int</c>, <c>long</c>, <c>double</c>.</typeparam>
        public struct TimeMarker<T1>
            where T1 : struct
        {
            internal readonly Timer<T1> Timer;

            private static long ticksPerMilliseconds = Stopwatch.Frequency / 1000;

            private readonly long ticks;

            internal TimeMarker(Timer<T1> timer)
            {
                this.Timer = timer;
                this.ticks = Stopwatch.GetTimestamp();
            }

            internal T1 ElapsedMilliseconds
            {
                get
                {
                    var ticks = Stopwatch.GetTimestamp();
                    var elapsedTicks = ticks - this.ticks;

                    T1 elapsed;
                    if (typeof(T1) == typeof(int))
                    {
                        elapsed = (T1)(object)(int)(elapsedTicks / TimeMarker<T1>.ticksPerMilliseconds);
                    }
                    else if (typeof(T1) == typeof(long))
                    {
                        elapsed = (T1)(object)(long)(elapsedTicks / TimeMarker<T1>.ticksPerMilliseconds);
                    }
                    else if (typeof(T1) == typeof(double))
                    {
                        elapsed = (T1)(object)(double)(elapsedTicks / TimeMarker<T1>.ticksPerMilliseconds);
                    }
                    else
                    {
                        throw new Exception("Unsupported Type");
                    }

                    return elapsed;
                }
            }
        }

        /// <summary>
        /// TimeMarkerWithTag records the start state of a Timer instrument.
        /// </summary>
        /// <typeparam name="T1">Support <c>int</c>, <c>long</c>, <c>double</c>.</typeparam>
        public struct TimeMarkerWithTag<T1> : IDisposable
            where T1 : struct
        {
            internal readonly TimeMarker<T1> TimerMark;
            private readonly KeyValuePair<string, object>[] tags;

            internal TimeMarkerWithTag(Timer<T1> timer, params KeyValuePair<string, object>[] tags)
            {
                this.TimerMark = new TimeMarker<T1>(timer);
                this.tags = tags;
            }

            /// <summary>
            /// Dispose and record elapsed time.
            /// </summary>
            public void Dispose()
            {
                this.TimerMark.Timer.RecordMeasurement(this.TimerMark.ElapsedMilliseconds, this.tags);
            }
        }
    }
}
