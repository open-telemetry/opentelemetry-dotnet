// <copyright file="ConcurrentIntrusiveList.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Utils
{
    using System;
    using System.Collections.Generic;

    internal sealed class ConcurrentIntrusiveList<T> where T : IElement<T>
    {
        private readonly object lck = new object();
        private int size = 0;
        private T head = default(T);

        public ConcurrentIntrusiveList()
        {
        }

        public int Count
        {
            get
            {
                return this.size;
            }
        }

        public void AddElement(T element)
        {
            lock (this.lck)
            {
                if (element.Next != null || element.Previous != null || element.Equals(this.head))
                {
                    throw new ArgumentOutOfRangeException("Element already in a list");
                }

                this.size++;
                if (this.head == null)
                {
                    this.head = element;
                }
                else
                {
                    this.head.Previous = element;
                    element.Next = this.head;
                    this.head = element;
                }
            }
        }

        public void RemoveElement(T element)
        {
            lock (this.lck)
            {
                if (element.Next == null && element.Previous == null && !element.Equals(this.head))
                {
                    throw new ArgumentOutOfRangeException("Element not in the list");
                }

                this.size--;
                if (element.Previous == null)
                {
                    // This is the first element
                    this.head = element.Next;
                    if (this.head != null)
                    {
                        // If more than one element in the list.
                        this.head.Previous = default(T);
                        element.Next = default(T);
                    }
                }
                else if (element.Next == null)
                {
                    // This is the last element, and there is at least another element because
                    // element.getPrev() != null.
                    element.Previous.Next = default(T);
                    element.Previous = default(T);
                }
                else
                {
                    element.Previous.Next = element.Next;
                    element.Next.Previous = element.Previous;
                    element.Next = default(T);
                    element.Previous = default(T);
                }
            }
        }

        public IReadOnlyList<T> Copy()
        {
            lock (this.lck)
            {
                List<T> all = new List<T>(this.size);
                for (T e = this.head; e != null; e = e.Next)
                {
                    all.Add(e);
                }

                return all;
            }
        }
    }
}
