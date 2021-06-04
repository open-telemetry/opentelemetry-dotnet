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

namespace OpenTelemetry.Metrics
{
    internal readonly struct Exemplar : IExemplar
    {
        private static readonly KeyValuePair<string, object>[] EmptyTag = new KeyValuePair<string, object>[0];
        private static readonly byte[] EmptyId = new byte[0];

        private readonly Type valueType;
        private readonly int intValue;
        private readonly double doubleValue;

        internal Exemplar(DateTimeOffset timestamp, int value, byte[] spanId, byte[] traceId, KeyValuePair<string, object>[] filteredTags)
        {
            this.Timestamp = timestamp;
            this.FilteredTags = filteredTags;
            this.SpanId = spanId;
            this.TraceId = traceId;
            this.valueType = value.GetType();
            this.intValue = value;
            this.doubleValue = 0;
        }

        internal Exemplar(DateTimeOffset timestamp, double value, byte[] spanId, byte[] traceId, KeyValuePair<string, object>[] filteredTags)
        {
            this.Timestamp = timestamp;
            this.FilteredTags = filteredTags;
            this.SpanId = spanId;
            this.TraceId = traceId;
            this.valueType = value.GetType();
            this.intValue = 0;
            this.doubleValue = value;
        }

        internal Exemplar(DateTimeOffset timestamp, int value)
            : this(timestamp, value, Exemplar.EmptyId, Exemplar.EmptyId, Exemplar.EmptyTag)
        {
        }

        internal Exemplar(DateTimeOffset timestamp, double value)
            : this(timestamp, value, Exemplar.EmptyId, Exemplar.EmptyId, Exemplar.EmptyTag)
        {
        }

        public DateTimeOffset Timestamp { get; }

        public readonly KeyValuePair<string, object>[] FilteredTags { get; }

        public readonly byte[] SpanId { get; }

        public readonly byte[] TraceId { get; }

        public object Value
        {
            get
            {
                if (this.valueType == typeof(int))
                {
                    return this.intValue;
                }
                else if (this.valueType == typeof(double))
                {
                    return this.doubleValue;
                }
                else
                {
                    throw new Exception("Unsupported Type");
                }
            }
        }

        internal static Exemplar CreateExemplar<T>(DateTimeOffset timestamp, T value, byte[] spanId, byte[] traceId, KeyValuePair<string, object>[] filteredTags)
        {
            Exemplar dp;

            if (typeof(T) == typeof(int))
            {
                dp = new Exemplar(timestamp, (int)(object)value, spanId, traceId, filteredTags);
            }
            else if (typeof(T) == typeof(double))
            {
                dp = new Exemplar(timestamp, (double)(object)value, spanId, traceId, filteredTags);
            }
            else
            {
                throw new Exception("Unsupported Type");
            }

            return dp;
        }
    }
}
