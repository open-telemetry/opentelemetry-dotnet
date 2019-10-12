// <copyright file="EvictingQueue.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections;
using System.Collections.Generic;

namespace OpenTelemetry.Trace.Internal
{
    internal class EvictingQueue<T> : IEnumerable<T>
    {
        private readonly int maxNumEvents;
        private readonly Queue<T> events;
        private int totalRecorded;

        public EvictingQueue(int maxNumEvents)
        {
            if (maxNumEvents < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumEvents), $"{nameof(maxNumEvents)} must be >= 0.");
            }

            this.maxNumEvents = maxNumEvents;
            this.events = new Queue<T>(maxNumEvents);
        }

        public int Count => this.events.Count;

        public int DroppedItems => this.totalRecorded - this.events.Count;

        public IEnumerator<T> GetEnumerator()
        {
            return this.events.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal void AddEvent(T evnt)
        {
            if (evnt == null)
            {
                throw new ArgumentNullException();
            }

            this.totalRecorded++;
            if (this.maxNumEvents == 0)
            {
                return;
            }

            if (this.events.Count == this.maxNumEvents)
            {
                this.events.Dequeue();
            }

            this.events.Enqueue(evnt);
        }
    }
}
