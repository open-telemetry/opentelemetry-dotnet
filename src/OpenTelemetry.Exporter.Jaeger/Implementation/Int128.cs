// <copyright file="Int128.cs" company="OpenTelemetry Authors">
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
using System;
using System.Diagnostics;

namespace OpenTelemetry.Exporter.Jaeger.Implementation
{
    public struct Int128
    {
        public static Int128 Empty = default;

        private const int SpanIdBytes = 8;
        private const int TraceIdBytes = 16;

        public Int128(ActivitySpanId spanId)
        {
            var bytes = new byte[SpanIdBytes];

            spanId.CopyTo(bytes);

            this.High = 0;
            this.Low = BitConverter.ToInt64(bytes, 0);
        }

        public Int128(ActivityTraceId traceId)
        {
            var bytes = new byte[TraceIdBytes];

            traceId.CopyTo(bytes);

            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
                this.High = BitConverter.ToInt64(bytes, 8);
                this.Low = BitConverter.ToInt64(bytes, 0);
            }
            else
            {
                this.High = BitConverter.ToInt64(bytes, 0);
                this.Low = BitConverter.ToInt64(bytes, 8);
            }
        }

        public long High { get; set; }

        public long Low { get; set; }
    }
}
