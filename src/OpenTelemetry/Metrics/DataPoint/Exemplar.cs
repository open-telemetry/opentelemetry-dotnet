// <copyright file="Exemplar.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;

namespace OpenTelemetry.Metrics
{
    internal readonly struct Exemplar : IExemplar
    {
        private static readonly KeyValuePair<string, object>[] EmptyTag = new KeyValuePair<string, object>[0];

        private readonly IDataValue value;

        internal Exemplar(DateTimeOffset timestamp, long value, ActivityTraceId traceId, ActivitySpanId spanId, KeyValuePair<string, object>[] filteredTags)
        {
            this.Timestamp = timestamp;
            this.FilteredTags = filteredTags;
            this.SpanId = spanId;
            this.TraceId = traceId;
            this.value = new DataValue<long>(value);
        }

        internal Exemplar(DateTimeOffset timestamp, double value, ActivityTraceId traceId, ActivitySpanId spanId, KeyValuePair<string, object>[] filteredTags)
        {
            this.Timestamp = timestamp;
            this.FilteredTags = filteredTags;
            this.SpanId = spanId;
            this.TraceId = traceId;
            this.value = new DataValue<double>(value);
        }

        internal Exemplar(DateTimeOffset timestamp, IDataValue value, ActivityTraceId traceId, ActivitySpanId spanId, KeyValuePair<string, object>[] filteredTags)
        {
            this.Timestamp = timestamp;
            this.FilteredTags = filteredTags;
            this.SpanId = spanId;
            this.TraceId = traceId;
            this.value = value;
        }

        internal Exemplar(DateTimeOffset timestamp, long value)
            : this(timestamp, value, default, default, Exemplar.EmptyTag)
        {
        }

        internal Exemplar(DateTimeOffset timestamp, double value)
            : this(timestamp, value, default, default, Exemplar.EmptyTag)
        {
        }

        internal Exemplar(DateTimeOffset timestamp, IDataValue value)
            : this(timestamp, value, default, default, Exemplar.EmptyTag)
        {
        }

        public DateTimeOffset Timestamp { get; }

        public readonly KeyValuePair<string, object>[] FilteredTags { get; }

        public readonly ActivityTraceId TraceId { get; }

        public readonly ActivitySpanId SpanId { get; }

        public object Value => this.value.Value;
    }
}
