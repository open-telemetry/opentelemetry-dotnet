// <copyright file="TraceEvents.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    using OpenCensus.Utils;

    internal class TraceEvents<T>
    {
        private readonly EvictingQueue<T> events;
        private int totalRecordedEvents = 0;

        public TraceEvents(int maxNumEvents)
        {
            this.events = new EvictingQueue<T>(maxNumEvents);
        }

        public EvictingQueue<T> Events
        {
            get
            {
                return this.events;
            }
        }

        public int NumberOfDroppedEvents
        {
            get { return this.totalRecordedEvents - this.events.Count; }
        }

        internal void AddEvent(T @event)
        {
            this.totalRecordedEvents++;
            this.events.Add(@event);
        }
    }
}
