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
        internal readonly T Value;

        private readonly DateTimeOffset timestamp;

        private readonly KeyValuePair<string, object>[] cachedTags;

        public DataPoint(T value, ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            this.timestamp = DateTimeOffset.UtcNow;
            this.Value = value;
            this.cachedTags = tags.ToArray();
        }

        public ReadOnlySpan<KeyValuePair<string, object>> Tags
        {
            get
            {
                return new ReadOnlySpan<KeyValuePair<string, object>>(this.cachedTags);
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

        public IDataPoint NewWithTags(ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            return new DataPoint<T>(this.Value, tags);
        }
    }
}
