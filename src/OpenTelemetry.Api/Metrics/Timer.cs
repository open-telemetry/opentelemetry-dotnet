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
        }

        /// <summary>
        /// Start a timer for a Timer instrument.
        /// </summary>
        /// <param name="tags">Attributes.</param>
        /// <returns>Timer (<see cref="TimerMarkWithTag{T}"/>) state.</returns>
        public TimerMarkWithTag<T> Start(params KeyValuePair<string, object>[] tags)
        {
            return new TimerMarkWithTag<T>(this, tags);
        }

        /// <summary>
        /// Stop and record time for a Timer instrument.
        /// </summary>
        /// <param name="mark">Timer state.</param>
        public void Stop(TimerMarkWithTag<T> mark)
        {
            mark.Dispose();
        }

        /// <summary>
        /// Start a timer for a Timer instrument.
        /// </summary>
        /// <returns>Timer (<see cref="TimerMark{T}"/>) state.</returns>
        public TimerMark<T> Start()
        {
            return new TimerMark<T>(this);
        }

        /// <summary>
        /// Stop and record time for a Timer instrument.
        /// </summary>
        /// <param name="mark">Timer state.</param>
        /// <param name="tags">Attributes.</param>
        public void Stop(TimerMark<T> mark, params KeyValuePair<string, object>[] tags)
        {
            mark.Watch.Stop();
            this.Record(mark.Watch.Elapsed, tags);
            mark.Watch = null;
        }

        internal void Record(TimeSpan elapsed, KeyValuePair<string, object>[] tags)
        {
            T value;

            if (typeof(T) == typeof(int))
            {
                value = (T)(object)(int)elapsed.TotalMilliseconds;
            }
            else if (typeof(T) == typeof(long))
            {
                value = (T)(object)(long)elapsed.TotalMilliseconds;
            }
            else if (typeof(T) == typeof(double))
            {
                value = (T)(object)(double)elapsed.TotalMilliseconds;
            }
            else
            {
                throw new Exception("Unsupported Type");
            }

            this.RecordMeasurement(value, tags);
        }

        /// <summary>
        /// TimerMark records the start state of a Timer instrument.
        /// </summary>
        /// <typeparam name="T1">Support <c>int</c>, <c>long</c>, <c>double</c>.</typeparam>
        public class TimerMark<T1>
            where T1 : struct
        {
            internal Timer<T1> Timer;
            internal Stopwatch Watch = new Stopwatch();

            internal TimerMark(Timer<T1> timer)
            {
                this.Timer = timer;
                this.Watch.Start();
            }
        }

        /// <summary>
        /// TimerMarkWithTag records the start state of a Timer instrument.
        /// </summary>
        /// <typeparam name="T1">Support <c>int</c>, <c>long</c>, <c>double</c>.</typeparam>
        public class TimerMarkWithTag<T1> : TimerMark<T1>, IDisposable
            where T1 : struct
        {
            internal KeyValuePair<string, object>[] Tags;

            internal TimerMarkWithTag(Timer<T1> timer, params KeyValuePair<string, object>[] tags)
                : base(timer)
            {
                this.Tags = tags;
            }

            /// <summary>
            /// Dispose and record elapsed time.
            /// </summary>
            public void Dispose()
            {
                this.Watch.Stop();
                this.Timer.Record(this.Watch.Elapsed, this.Tags);
                this.Watch = null;
            }
        }
    }
}
