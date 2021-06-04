// <copyright file="Exemplar{T}.cs" company="OpenTelemetry Authors">
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
    internal readonly struct Exemplar<T> : IExemplar
        where T : struct
    {
        private static readonly KeyValuePair<string, object>[] EmptyTag = new KeyValuePair<string, object>[0];
        private static readonly byte[] EmptyId = new byte[0];

        private readonly T value;

        internal Exemplar(DateTimeOffset timestamp, T value, byte[] spanId, byte[] traceId, KeyValuePair<string, object>[] filteredTags)
        {
            this.Timestamp = timestamp;
            this.FilteredTags = filteredTags;
            this.SpanId = spanId;
            this.TraceId = traceId;
            this.value = value;
        }

        internal Exemplar(DateTimeOffset timestamp, T value)
            : this(timestamp, value, Exemplar<T>.EmptyId, Exemplar<T>.EmptyId, Exemplar<T>.EmptyTag)
        {
        }

        public DateTimeOffset Timestamp { get; }

        public readonly KeyValuePair<string, object>[] FilteredTags { get; }

        public readonly byte[] SpanId { get; }

        public readonly byte[] TraceId { get; }

        public object Value => (object)this.value;
    }
}
