// <copyright file="Timestamp.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Common
{
    using System;
    using System.Diagnostics;

    /// <summary>
    /// Timestamp with the nanoseconds precision.
    /// </summary>
    [DebuggerDisplay("{ToString(),nq}")]
    public sealed class Timestamp : IComparable<Timestamp>, IComparable
    {
        /// <summary>
        /// Represents zero timestamp.
        /// </summary>
        public static readonly Timestamp Zero = new Timestamp(0, 0);

        private const long MaxSeconds = 315576000000L;
        private const int MaxNanos = 999999999;
        private const long MillisPerSecond = 1000L;
        private const long NanosPerMilli = 1000 * 1000;
        private const long NanosPerSecond = NanosPerMilli * MillisPerSecond;
        private readonly string stringRepresentation;

        internal Timestamp(long seconds, int nanos)
        {
            this.Seconds = seconds;
            this.Nanos = nanos;
            this.stringRepresentation = $"Timestamp{{seconds={this.Seconds}, nanos={this.Nanos}}}";
        }

        /// <summary>
        /// Gets the number of seconds since the Unix Epoch represented by this timestamp.
        /// </summary>
        public long Seconds { get; }

        /// <summary>
        /// Gets the the number of nanoseconds after the number of seconds since the Unix Epoch represented
        /// by this timestamp.
        /// </summary>
        public int Nanos { get; }

        /// <summary>
        /// Creates an instance of <see cref="Timestamp" /> class with the given seconds and nanoseconds values.
        /// </summary>
        /// <param name="seconds">Total number of seconds since the Unix Epoch represented by this <see cref="Timestamp"/>.</param>
        /// <param name="nanos">The number of nanoseconds after the number of seconds since the Unix Epoch represented by this <see cref="Timestamp"/>.</param>
        /// <returns>New instance of <see cref="Timestamp"/>.</returns>
        public static Timestamp Create(long seconds, int nanos)
        {
            if (seconds < -MaxSeconds || seconds > MaxSeconds)
            {
                return Zero;
            }

            if (nanos < 0 || nanos > MaxNanos)
            {
                return Zero;
            }

            return new Timestamp(seconds, nanos);
        }

        /// <summary>
        /// Creates an instance of <see cref="Timestamp" /> class with the given total milliseconds since Unix Epoch.
        /// </summary>
        /// <param name="millis">Total number of milliseconds since the Unix Epoch represented by this <see cref="Timestamp"/>.</param>
        /// <returns>New instance of <see cref="Timestamp"/>.</returns>
        public static Timestamp FromMillis(long millis)
        {
            var nanos = millis * NanosPerMilli;
            return Zero.Plus(0, nanos);
        }

        /// <summary>
        /// Creates an instance of <see cref="Timestamp" /> class with the given time as <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <param name="time">Time to convert to <see cref="Timestamp"/>.</param>
        /// <returns>New instance of <see cref="Timestamp"/>.</returns>
        public static Timestamp FromDateTimeOffset(DateTimeOffset time)
        {
            long seconds = 0;
#if NET45
            var unixZero = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero);
            seconds = (int)Math.Floor(time.Subtract(unixZero).TotalSeconds);
#else
            seconds = time.ToUnixTimeSeconds();
#endif

            var nanos = (int)time.Subtract(new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero)).Subtract(TimeSpan.FromSeconds(seconds)).Ticks * 100;
            return Timestamp.Create(seconds, nanos);
        }

        /// <summary>
        /// Adds duration to the timestamp.
        /// </summary>
        /// <param name="duration">Duration to add to the timestamp.</param>
        /// <returns>Returns the timestamp with added duration.</returns>
        public Timestamp AddDuration(Duration duration)
        {
            return this.Plus(duration.Seconds, duration.Nanos);
        }

        /// <summary>
        /// Adds nanosToAdd nanosecond to the current timestamp.
        /// </summary>
        /// <param name="nanosToAdd">Number of nanoseconds to add.</param>
        /// <returns>Returns the timstemp with added nanoseconds.</returns>
        public Timestamp AddNanos(long nanosToAdd)
        {
            return this.Plus(0, nanosToAdd);
        }

        /// <summary>
        /// Substructs timestamp from the current timestamp. Typically to calculate duration.
        /// </summary>
        /// <param name="timestamp">Timestamp to substruct.</param>
        /// <returns>Returns the timestamp with the substructed duration.</returns>
        public Duration SubtractTimestamp(Timestamp timestamp)
        {
            var durationSeconds = this.Seconds - timestamp.Seconds;
            var durationNanos = this.Nanos - timestamp.Nanos;
            if (durationSeconds < 0 && durationNanos > 0)
            {
                durationSeconds += 1;
                durationNanos = (int)(durationNanos - NanosPerSecond);
            }
            else if (durationSeconds > 0 && durationNanos < 0)
            {
                durationSeconds -= 1;
                durationNanos = (int)(durationNanos + NanosPerSecond);
            }

            return Duration.Create(durationSeconds, durationNanos);
        }

        /// <inheritdoc />
        public int CompareTo(Timestamp other)
        {
            var cmp = (this.Seconds < other.Seconds) ? -1 : ((this.Seconds > other.Seconds) ? 1 : 0);
            if (cmp != 0)
            {
                return cmp;
            }

            return (this.Nanos < other.Nanos) ? -1 : ((this.Nanos > other.Nanos) ? 1 : 0);
        }

        /// <inheritdoc />
        public int CompareTo(object obj)
        {
            if (obj is Timestamp timestamp)
            {
                return this.CompareTo(timestamp);
            }
            else
            {
                throw new InvalidOperationException();
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.stringRepresentation;
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o is Timestamp that)
            {
                return (this.Seconds == that.Seconds)
                     && (this.Nanos == that.Nanos);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            long h = 1;
            h *= 1000003;
            h ^= (this.Seconds >> 32) ^ this.Seconds;
            h *= 1000003;
            h ^= this.Nanos;
            return (int)h;
        }

        private static Timestamp OfSecond(long seconds, long nanoAdjustment)
        {
            var floor = (long)Math.Floor((double)nanoAdjustment / NanosPerSecond);
            var secs = seconds + floor;
            var nos = nanoAdjustment - (floor * NanosPerSecond);
            return Create(secs, (int)nos);
        }

        private Timestamp Plus(long secondsToAdd, long nanosToAdd)
        {
            if ((secondsToAdd | nanosToAdd) == 0)
            {
                return this;
            }

            var sec = this.Seconds + secondsToAdd;
            var nanoSeconds = Math.DivRem(nanosToAdd, NanosPerSecond, out var nanosSpill);
            sec += nanoSeconds;
            var nanoAdjustment = this.Nanos + nanosSpill;
            return OfSecond(sec, nanoAdjustment);
        }
    }
}
