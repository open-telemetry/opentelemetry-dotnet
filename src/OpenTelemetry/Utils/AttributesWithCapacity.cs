// <copyright file="AttributesWithCapacity.cs" company="OpenTelemetry Authors">
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
    using System.Collections.Generic;

    internal class AttributesWithCapacity
    {
        private readonly int capacity;

        private KeyValueListNode attributesHead = null;
        private KeyValueListNode attributesTail = null;

        private int totalRecordedAttributes;
        private int count = 0;

        public AttributesWithCapacity(int capacity)
        {
            this.capacity = capacity;
        }

        public int NumberOfDroppedAttributes => this.totalRecordedAttributes - this.count;

        public void PutAttribute(string key, object value)
        {
            this.totalRecordedAttributes += 1;
            if (this.capacity == 0)
            {
                return;
            }

            this.count++;
            var next = new KeyValueListNode
            {
                KeyValue = new KeyValuePair<string, object>(key, value),
                Next = null,
            }; 

            if (this.attributesHead == null)
            {
                this.attributesHead = next;
                this.attributesTail = next;
            }
            else
            {
                this.attributesTail.Next = next;
                this.attributesTail = next;
                if (this.count > this.capacity)
                {
                    this.attributesHead = this.attributesHead.Next;
                    this.count--;
                }
            }
        }

        public IReadOnlyCollection<KeyValuePair<string, object>> AsReadOnlyCollection()
        {
            var result = new List<KeyValuePair<string, object>>();
            var next = this.attributesHead;

            while (next != null)
            {
                result.Add(new KeyValuePair<string, object>(next.KeyValue.Key, next.KeyValue.Value));
                next = next.Next;
            }

            return result;
        }

        /// <summary>
        /// Having our own key-value linked list allows us to be more efficient.  
        /// </summary>
        private class KeyValueListNode
        {
            public KeyValuePair<string, object> KeyValue;
            public KeyValueListNode Next;
        }
    }
}
