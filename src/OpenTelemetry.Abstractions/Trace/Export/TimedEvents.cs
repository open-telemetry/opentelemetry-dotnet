// <copyright file="TimedEvents.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;
    using System.Linq;

    public sealed class TimedEvents<T> : ITimedEvents<T>
    {
        public static readonly ITimedEvents<T> Empty = new TimedEvents<T>(new ITimedEvent<T>[0], 0);

        internal TimedEvents(IEnumerable<ITimedEvent<T>> events, int droppedEventsCount)
        {
            this.Events = events ?? throw new ArgumentNullException("Null events");
            this.DroppedEventsCount = droppedEventsCount;
        }

        public IEnumerable<ITimedEvent<T>> Events { get; }

        public int DroppedEventsCount { get; }

        public static ITimedEvents<T> Create(IReadOnlyCollection<ITimedEvent<T>> events, int droppedEventsCount)
        {
            if (events == null)
            {
                return Empty;
            }

            return new TimedEvents<T>(events, droppedEventsCount);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "TimedEvents"
                + "{"
                + "events=" + this.Events + ", "
                + "droppedEventsCount=" + this.DroppedEventsCount
                + "}";
        }

        /// <inheritdoc/>
        public override bool Equals(object o)
        {
            if (o == this)
            {
                return true;
            }

            if (o is TimedEvents<T> that)
            {
                return this.Events.SequenceEqual(that.Events)
                     && (this.DroppedEventsCount == that.DroppedEventsCount);
            }

            return false;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            var h = 1;
            h *= 1000003;
            h ^= this.Events.GetHashCode();
            h *= 1000003;
            h ^= this.DroppedEventsCount;
            return h;
        }
    }
}
