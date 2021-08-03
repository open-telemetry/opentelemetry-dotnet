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
    internal readonly struct DataPoint : IDataPoint
    {
        private static readonly KeyValuePair<string, object>[] EmptyTag = new KeyValuePair<string, object>[0];

        private readonly IDataValue value;

        internal DataPoint(DateTimeOffset timestamp, long value, KeyValuePair<string, object>[] tags)
        {
            this.Timestamp = timestamp;
            this.Tags = tags;
            this.value = new DataValue<long>(value);
        }

        internal DataPoint(DateTimeOffset timestamp, double value, KeyValuePair<string, object>[] tags)
        {
            this.Timestamp = timestamp;
            this.Tags = tags;
            this.value = new DataValue<double>(value);
        }

        internal DataPoint(DateTimeOffset timestamp, IDataValue value, KeyValuePair<string, object>[] tags)
        {
            this.Timestamp = timestamp;
            this.Tags = tags;
            this.value = value;
        }

        internal DataPoint(DateTimeOffset timestamp, long value)
            : this(timestamp, value, DataPoint.EmptyTag)
        {
        }

        internal DataPoint(DateTimeOffset timestamp, double value)
            : this(timestamp, value, DataPoint.EmptyTag)
        {
        }

        internal DataPoint(DateTimeOffset timestamp, IDataValue value)
            : this(timestamp, value, DataPoint.EmptyTag)
        {
        }

        public DateTimeOffset Timestamp { get; }

        public readonly KeyValuePair<string, object>[] Tags { get; }

        public object Value => this.value.Value;
    }
}
