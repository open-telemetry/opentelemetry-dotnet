// <copyright file="BinaryFormatTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation.Test
{
    using System.Diagnostics;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;
    using System;
    using Xunit;

    public class BinaryFormatTest
    {
        private static readonly byte[] TraceIdBytes = new byte[] { 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79 };
        private static readonly ActivityTraceId TraceId = ActivityTraceId.CreateFromBytes(TraceIdBytes);
        private static readonly byte[] SpanIdBytes = new byte[] { 97, 98, 99, 100, 101, 102, 103, 104 };
        private static readonly ActivitySpanId SpanId = ActivitySpanId.CreateFromBytes(SpanIdBytes);
        private static readonly byte[] TraceOptionsBytes = new byte[] { 1 };
        private static readonly ActivityTraceFlags TraceOptions = ActivityTraceFlags.Recorded;
        private static readonly byte[] ExampleBytes =
            new byte[] {0, 0, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 1, 97, 98, 99, 100,101, 102, 103, 104, 2, 1};

        private static readonly SpanContext ExampleSpanContext = SpanContext.Create(TraceId, SpanId, TraceOptions, Tracestate.Empty);

        private readonly BinaryFormat binaryFormat = new BinaryFormat();

        private void TestSpanContextConversion(SpanContext spanContext)
        {
            var propagatedBinarySpanContext = binaryFormat.FromByteArray(binaryFormat.ToByteArray(spanContext));
            Assert.Equal(spanContext, propagatedBinarySpanContext);
        }

        [Fact]
        public void Propagate_SpanContextTracingEnabled()
        {
            TestSpanContextConversion(SpanContext.Create(TraceId, SpanId, ActivityTraceFlags.Recorded, Tracestate.Empty));
        }

        [Fact]
        public void Propagate_SpanContextNoTracing()
        {
            TestSpanContextConversion(SpanContext.Create(TraceId, SpanId, ActivityTraceFlags.None, Tracestate.Empty));
        }

        [Fact]
        public void ToBinaryValue_NullSpanContext()
        {
            Assert.Throws<ArgumentNullException>(() => binaryFormat.ToByteArray(null));
        }

        [Fact]
        public void ToBinaryValue_InvalidSpanContext()
        {
            Assert.Equal(
                new byte[] {0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 2, 0},
                binaryFormat.ToByteArray(SpanContext.Blank));

        }

        [Fact]
        public void FromBinaryValue_BinaryExampleValue()
        {
            Assert.Equal(ExampleSpanContext, binaryFormat.FromByteArray(ExampleBytes));
        }

        [Fact]
        public void FromBinaryValue_NullInput()
        {
            Assert.Throws<ArgumentNullException>(() => binaryFormat.FromByteArray(null));
        }

        [Fact]
        public void FromBinaryValue_EmptyInput()
        {
            Assert.Throws<SpanContextParseException>(() => binaryFormat.FromByteArray(new byte[0]));
        }

        [Fact]
        public void FromBinaryValue_UnsupportedVersionId()
        {
            Assert.Throws<SpanContextParseException>(() => binaryFormat.FromByteArray(
                new byte[] 
                {
                    66, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 97, 98, 99, 100, 101,
                    102, 103, 104, 1,
                }));
        }

        [Fact]
        public void FromBinaryValue_UnsupportedFieldIdFirst()
        {
            Assert.Equal(
                SpanContext.Create(default, default, ActivityTraceFlags.None, Tracestate.Empty),
                binaryFormat.FromByteArray(
                    new byte[] 
                    {
                        0, 4, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 1, 97, 98,
                        99, 100, 101, 102, 103, 104, 2, 1,
                    }));
        }

        [Fact]
        public void FromBinaryValue_UnsupportedFieldIdSecond()
        {
            Assert.Equal(
                 SpanContext.Create(
                        ActivityTraceId.CreateFromBytes(new byte[] { 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79 }),
                        default,
                        ActivityTraceFlags.None, Tracestate.Empty),
                 binaryFormat.FromByteArray(
                        new byte[] 
                        {
                            0, 0, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 3, 97, 98,
                            99, 100, 101, 102, 103, 104, 2, 1,
                        }));
        }

        [Fact]
        public void FromBinaryValue_ShorterTraceId()
        {
            Assert.Throws<SpanContextParseException>(() => binaryFormat.FromByteArray(
                new byte[] { 0, 0, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76 }));
        }

        [Fact]
        public void FromBinaryValue_ShorterSpanId()
        {
            Assert.Throws<SpanContextParseException>(() => binaryFormat.FromByteArray(new byte[] { 0, 1, 97, 98, 99, 100, 101, 102, 103 }));
        }

        [Fact]
        public void FromBinaryValue_ShorterTraceOptions()
        {
            Assert.Throws<SpanContextParseException>(() => binaryFormat.FromByteArray(
                new byte[] {
                    0, 0, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 1, 97, 98, 99, 100,
                    101, 102, 103, 104, 2,}));
        }
    }
}
