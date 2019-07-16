// <copyright file="TimedEvent.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Export
{
    using System;
    using OpenTelemetry.Abstractions.Utils;

    public sealed class TimedEvent<T> : ITimedEvent<T>
    {
        internal TimedEvent(DateTime timestamp, T @event)
        {
            this.Timestamp = timestamp;
            this.Event = @event;
        }

        public DateTime Timestamp { get; }

        public T Event { get; }

        public static ITimedEvent<T> Create(DateTime timestamp, T @event)
        {
            return timestamp == default
                ? new TimedEvent<T>(PreciseTimestamp.GetUtcNow(), @event)
                : new TimedEvent<T>(timestamp, @event);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "TimedEvent{"
                + "timestamp=" + this.Timestamp + ", "
                + "event=" + this.Event
                + "}";
        }

    /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is TimedEvent<T> that)
            {
                return this.Timestamp.Equals(that.Timestamp)
                     && this.Event.Equals(that.Event);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.Timestamp.GetHashCode();
            h *= 1000003;
            h ^= this.Event.GetHashCode();
            return h;
        }
    }
}
