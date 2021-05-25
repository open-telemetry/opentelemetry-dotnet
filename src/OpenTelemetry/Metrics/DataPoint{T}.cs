// <copyright file="DataPoint{T}.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;

namespace OpenTelemetry.Metrics
{
    internal readonly struct DataPoint<T> : IDataPoint
        where T : struct
    {
        private readonly T value;

        private readonly DateTimeOffset timestamp;

        private readonly KeyValuePair<string, object>[] tags;

        public DataPoint(DateTimeOffset timestamp, T value, KeyValuePair<string, object>[] tags)
        {
            this.timestamp = timestamp;
            this.value = value;
            this.tags = tags;
        }

        public KeyValuePair<string, object>[] Tags
        {
            get
            {
                return this.tags;
            }
        }

        public DateTimeOffset Timestamp
        {
            get
            {
                return this.timestamp;
            }
        }

        public object Value
        {
            get
            {
                return (object)this.value;
            }
        }
    }
}
