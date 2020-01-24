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
using System.Diagnostics;

namespace OpenTelemetry.Trace.Internal
{
    internal class EvictingQueue<T> : IEnumerable<T>
    {
        private readonly int maxNumItems;
        private readonly T[] items;
        private int totalRecorded;
        private int tail;

        public EvictingQueue(int maxNumItems)
        {
            if (maxNumItems < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxNumItems), $"{nameof(maxNumItems)} must be >= 0.");
            }

            this.maxNumItems = maxNumItems;
            this.tail = 0;
            this.items = new T[maxNumItems];
        }

        public int Count { get; private set; }

        public int DroppedItems => this.totalRecorded - this.Count;

        public IEnumerator<T> GetEnumerator()
        {
            var head = this.tail - this.Count;
            for (int i = head; i < this.Count + head; i++)
            {
                yield return this.items[i % this.maxNumItems];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        internal void Add(T item)
        {
            Debug.Assert(item != null, "Item must not be null");

            this.totalRecorded++;
            if (this.maxNumItems == 0)
            {
                return;
            }

            if (this.Count < this.maxNumItems)
            {
                this.Count++;
            }

            this.items[this.tail % this.maxNumItems] = item;
            this.tail++;
        }
    }
}
