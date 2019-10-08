// <copyright file="BinaryFormat.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using OpenTelemetry.Trace;

    public class BinaryFormat : IBinaryFormat
    {
        private const byte VersionId = 0;
        private const int VersionIdOffset = 0;
        private const int TraceIdSize = 16;
        private const int SpanIdSize = 8;
        private const int TraceOptionsSize = 1;

        // The version_id/field_id size in bytes.
        private const byte IdSize = 1;
        private const byte TraceIdFieldId = 0;
        private const int TraceIdFieldIdOffset = VersionIdOffset + IdSize;
        private const int TraceIdOffset = TraceIdFieldIdOffset + IdSize;
        private const byte SpanIdFieldId = 1;
        private const int SpanIdFieldIdOffset = TraceIdOffset + TraceIdSize;
        private const int SpanIdOffset = SpanIdFieldIdOffset + IdSize;
        private const byte TraceOptionsFieldId = 2;
        private const int TraceOptionFieldIdOffset = SpanIdOffset + SpanIdSize;
        private const int TraceOptionOffset = TraceOptionFieldIdOffset + IdSize;
        private const int FormatLength = (4 * IdSize) + TraceIdSize + SpanIdSize + TraceOptionsSize;

        public SpanContext FromByteArray(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length == 0 || bytes[0] != VersionId)
            {
                throw new SpanContextParseException("Unsupported version.");
            }

            ActivityTraceId traceId = default;
            ActivitySpanId spanId = default;
            var traceOptions = ActivityTraceFlags.None;

            var traceparentBytes = new ReadOnlySpan<byte>(bytes);
            var pos = 1;
            try
            {
                if (bytes.Length > pos && bytes[pos] == TraceIdFieldId)
                {
                    traceId = ActivityTraceId.CreateFromBytes(traceparentBytes.Slice(pos + IdSize, 16));
                    pos += IdSize + TraceIdSize;
                }

                if (bytes.Length > pos && bytes[pos] == SpanIdFieldId)
                {
                    spanId = ActivitySpanId.CreateFromBytes(traceparentBytes.Slice(pos + IdSize, 8));
                    pos += IdSize + SpanIdSize;
                }

                if (bytes.Length > pos && bytes[pos] == TraceOptionsFieldId)
                {
                    traceOptions = (ActivityTraceFlags)traceparentBytes[pos + IdSize];
                }

                return new SpanContext(traceId, spanId, traceOptions);
            }
            catch (Exception e)
            {
                throw new SpanContextParseException("Invalid input.", e);
            }
        }

        public byte[] ToByteArray(SpanContext spanContext)
        {
            if (spanContext == null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            Span<byte> spanBytes = stackalloc byte[FormatLength];
            spanBytes[VersionIdOffset] = VersionId;
            spanBytes[TraceIdFieldIdOffset] = TraceIdFieldId;
            spanBytes[SpanIdFieldIdOffset] = SpanIdFieldId;
            spanBytes[TraceOptionFieldIdOffset] = TraceOptionsFieldId;
            spanBytes[TraceOptionOffset] = (byte)spanContext.TraceOptions;
            spanContext.TraceId.CopyTo(spanBytes.Slice(TraceIdOffset, TraceIdSize));
            spanContext.SpanId.CopyTo(spanBytes.Slice(SpanIdOffset, SpanIdSize));

            return spanBytes.ToArray();
        }
    }
}
