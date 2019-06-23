// <copyright file="Duration.cs" company="OpenTelemetry Authors">
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
    /// Represents duration with the nanoseconds precition.
    /// </summary>
    [DebuggerDisplay("{ToString(),nq}")]
    public sealed class Duration : IComparable<Duration>
    {
        private const long MaxSeconds = 315576000000L;
        private const int MaxNanos = 999999999;
        private static readonly Duration Zero = new Duration(0, 0);
        private readonly string stringRepresentation;

        private Duration(long seconds, int nanos)
        {
            this.Seconds = seconds;
            this.Nanos = nanos;
            this.stringRepresentation = $"Duration{{seconds={this.Seconds}, nanos={this.Nanos}}}";
        }

        /// <summary>
        /// Gets the number of second in duration.
        /// </summary>
        public long Seconds { get; }

        /// <summary>
        /// Gets the number of nanoseconds in duration.
        /// </summary>
        public int Nanos { get; }

        /// <summary>
        /// Creates a new instance of <see cref="Duration" /> class.
        /// </summary>
        /// <param name="seconds">Total seconds.</param>
        /// <param name="nanos">Nanoseconds part of a duration up to 999999999.</param>
        /// <returns>New instance of <see cref="Duration" /> class.</returns>
        public static Duration Create(long seconds, int nanos)
        {
            if (seconds < -MaxSeconds || seconds > MaxSeconds)
            {
                return Zero;
            }

            if (nanos < -MaxNanos || nanos > MaxNanos)
            {
                return Zero;
            }

            if ((seconds < 0 && nanos > 0) || (seconds > 0 && nanos < 0))
            {
                return Zero;
            }

            return new Duration(seconds, nanos);
        }

        /// <summary>
        /// Creates a new instance of <see cref="Duration" /> class.
        /// </summary>
        /// <param name="duration">Duration as TimeStamp.</param>
        /// <returns>New instance of <see cref="Duration" /> class.</returns>
        public static Duration Create(TimeSpan duration)
        {
            var seconds = duration.Ticks / TimeSpan.TicksPerSecond;
            var nanoseconds = (int)(duration.Ticks % TimeSpan.TicksPerSecond) * 100;
            return Create(seconds, nanoseconds);
        }

        /// <summary>
        /// Compares durations.
        /// </summary>
        /// <param name="other"><see cref="Duration" /> instasnce to compare to.</param>
        /// <returns>Zero if equal, -1 when lesser and +1 when greater than given value.</returns>
        public int CompareTo(Duration other)
        {
            var cmp = (this.Seconds < other.Seconds) ? -1 : ((this.Seconds > other.Seconds) ? 1 : 0);
            if (cmp != 0)
            {
                return cmp;
            }

            return (this.Nanos < other.Nanos) ? -1 : ((this.Nanos > other.Nanos) ? 1 : 0);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return this.stringRepresentation;
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is Duration that)
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
    }
}
