// <copyright file="Int128.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    internal readonly struct Int128
    {
        public static Int128 Empty;

        private const int SpanIdBytes = 8;
        private const int TraceIdBytes = 16;

        public Int128(ActivitySpanId spanId)
        {
            Span<byte> bytes = stackalloc byte[SpanIdBytes];
            spanId.CopyTo(bytes);

            if (BitConverter.IsLittleEndian)
            {
                bytes.Reverse();
            }

            var longs = MemoryMarshal.Cast<byte, long>(bytes);
            this.High = 0;
            this.Low = longs[0];
        }

        public Int128(ActivityTraceId traceId)
        {
            Span<byte> bytes = stackalloc byte[TraceIdBytes];
            traceId.CopyTo(bytes);

            if (BitConverter.IsLittleEndian)
            {
                bytes.Reverse();
            }

            var longs = MemoryMarshal.Cast<byte, long>(bytes);
            this.High = BitConverter.IsLittleEndian ? longs[1] : longs[0];
            this.Low = BitConverter.IsLittleEndian ? longs[0] : longs[1];
        }

        public long High { get; }

        public long Low { get; }
    }
}
