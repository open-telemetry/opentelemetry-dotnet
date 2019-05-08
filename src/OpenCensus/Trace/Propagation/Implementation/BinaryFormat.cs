// <copyright file="BinaryFormat.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace.Propagation.Implementation
{
    using System;

    internal class BinaryFormat : BinaryFormatBase
    {
        private const byte VersionId = 0;
        private const int VersionIdOffset = 0;

        // The version_id/field_id size in bytes.
        private const byte IdSize = 1;
        private const byte TraceIdFieldId = 0;
        private const int TraceIdFieldIdOffset = VersionIdOffset + IdSize;
        private const int TraceIdOffset = TraceIdFieldIdOffset + IdSize;
        private const byte SpanIdFieldId = 1;
        private const int SpaneIdFieldIdOffset = TraceIdOffset + TraceId.Size;
        private const int SpanIdOffset = SpaneIdFieldIdOffset + IdSize;
        private const byte TraceOptionsFieldId = 2;
        private const int TraceOptionFieldIdOffset = SpanIdOffset + SpanId.Size;
        private const int TraceOptionOffset = TraceOptionFieldIdOffset + IdSize;
        private const int FormatLength = (4 * IdSize) + TraceId.Size + SpanId.Size + TraceOptions.Size;

        public override ISpanContext FromByteArray(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            if (bytes.Length == 0 || bytes[0] != VersionId)
            {
                throw new SpanContextParseException("Unsupported version.");
            }

            ITraceId traceId = TraceId.Invalid;
            ISpanId spanId = SpanId.Invalid;
            TraceOptions traceOptions = TraceOptions.Default;

            int pos = 1;
            try
            {
                if (bytes.Length > pos && bytes[pos] == TraceIdFieldId)
                {
                    traceId = TraceId.FromBytes(bytes, pos + IdSize);
                    pos += IdSize + TraceId.Size;
                }

                if (bytes.Length > pos && bytes[pos] == SpanIdFieldId)
                {
                    spanId = SpanId.FromBytes(bytes, pos + IdSize);
                    pos += IdSize + SpanId.Size;
                }

                if (bytes.Length > pos && bytes[pos] == TraceOptionsFieldId)
                {
                    traceOptions = TraceOptions.FromBytes(bytes, pos + IdSize);
                }

                return SpanContext.Create(traceId, spanId, traceOptions, Tracestate.Empty);
            }
            catch (Exception e)
            {
                throw new SpanContextParseException("Invalid input.", e);
            }
        }

        public override byte[] ToByteArray(ISpanContext spanContext)
        {
            if (spanContext == null)
            {
                throw new ArgumentNullException(nameof(spanContext));
            }

            byte[] bytes = new byte[FormatLength];
            bytes[VersionIdOffset] = VersionId;
            bytes[TraceIdFieldIdOffset] = TraceIdFieldId;
            spanContext.TraceId.CopyBytesTo(bytes, TraceIdOffset);
            bytes[SpaneIdFieldIdOffset] = SpanIdFieldId;
            spanContext.SpanId.CopyBytesTo(bytes, SpanIdOffset);
            bytes[TraceOptionFieldIdOffset] = TraceOptionsFieldId;
            spanContext.TraceOptions.CopyBytesTo(bytes, TraceOptionOffset);
            return bytes;
        }
    }
}
