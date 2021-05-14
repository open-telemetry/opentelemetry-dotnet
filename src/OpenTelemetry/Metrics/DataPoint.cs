// <copyright file="DataPoint.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics
{
    internal struct DataPoint<T> : IDataPoint
        where T : struct
    {
        internal T Value;

        private DateTimeOffset timestamp;

        private KeyValuePair<string, object>[] tags;
        private KeyValuePair<string, object>[] sortedTags;

        public DataPoint(T value, KeyValuePair<string, object>[] tags)
        {
            this.timestamp = DateTimeOffset.UtcNow;
            this.Value = value;
            this.tags = tags;
            this.sortedTags = null;
        }

        public KeyValuePair<string, object>[] Tags
        {
            get
            {
                return this.tags;
            }
        }

        public KeyValuePair<string, object>[] SortedTags
        {
            get
            {
                if (this.sortedTags == null)
                {
                    if (this.tags.Length <= 1)
                    {
                        this.sortedTags = this.tags;
                    }
                    else
                    {
                        this.sortedTags = new KeyValuePair<string, object>[this.tags.Length];
                        this.tags.CopyTo(this.sortedTags, 0);
                        Array.Sort(this.sortedTags, (x, y) => x.Key.CompareTo(y.Key));
                    }
                }

                return this.sortedTags;
            }
        }

        public DateTimeOffset Timestamp
        {
            get
            {
                return this.timestamp;
            }
        }

        public string ValueAsString
        {
            get
            {
                return this.Value.ToString();
            }
        }

        public void Reset<T1>(T1 value, KeyValuePair<string, object>[] tags)
            where T1 : struct
        {
            this.timestamp = DateTimeOffset.UtcNow;
            this.tags = tags;
            this.sortedTags = null;

            if (typeof(T1) == typeof(T))
            {
                this.Value = (T)((object)value);
            }
        }

        public void ResetTags(KeyValuePair<string, object>[] tags)
        {
            this.tags = tags;
            this.sortedTags = null;
        }

        public IDataPoint NewWithValue()
        {
            return new DataPoint<T>(this.Value, new KeyValuePair<string, object>[0]);
        }
    }
}
