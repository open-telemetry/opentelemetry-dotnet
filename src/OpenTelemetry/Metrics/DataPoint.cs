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

#nullable enable

namespace OpenTelemetry.Metrics
{
    internal struct DataPoint<T> : IDataPoint
        where T : struct
    {
        internal readonly T Value;

        [ThreadStatic]
        private static KeyValuePair<string, object?>[] cachedTags = new KeyValuePair<string, object?>[100];

        private readonly DateTimeOffset timestamp;

        private int cachedTagsLen;

        public DataPoint(T value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            if (DataPoint<T>.cachedTags == null)
            {
                DataPoint<T>.cachedTags = new KeyValuePair<string, object?>[100];
            }

            this.timestamp = DateTimeOffset.UtcNow;
            this.Value = value;

            int i = 0;
            foreach (var tag in tags)
            {
                cachedTags[i] = tag;
                i++;
            }

            this.cachedTagsLen = i;
        }

        public ReadOnlySpan<KeyValuePair<string, object?>> Tags
        {
            get
            {
                return new ReadOnlySpan<KeyValuePair<string, object?>>(DataPoint<T>.cachedTags, 0, this.cachedTagsLen);
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

        public IDataPoint NewWithTags(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            return new DataPoint<T>(this.Value, tags);
        }
    }
}
