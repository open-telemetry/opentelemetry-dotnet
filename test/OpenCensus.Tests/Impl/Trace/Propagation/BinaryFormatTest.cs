// <copyright file="BinaryFormatTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Propagation.Test
{
    using OpenCensus.Trace.Propagation.Implementation;
    using System;
    using Xunit;

    public class BinaryFormatTest
    {
        private static readonly byte[] TRACE_ID_BYTES = new byte[] { 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79 };
        private static readonly ITraceId TRACE_ID = TraceId.FromBytes(TRACE_ID_BYTES);
        private static readonly byte[] SPAN_ID_BYTES = new byte[] { 97, 98, 99, 100, 101, 102, 103, 104 };
        private static readonly ISpanId SPAN_ID = SpanId.FromBytes(SPAN_ID_BYTES);
        private static readonly byte[] TRACE_OPTIONS_BYTES = new byte[] { 1 };
        private static readonly TraceOptions TRACE_OPTIONS = TraceOptions.FromBytes(TRACE_OPTIONS_BYTES);
        private static readonly byte[] EXAMPLE_BYTES =
            new byte[] {0, 0, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 1, 97, 98, 99, 100,101, 102, 103, 104, 2, 1};

        private static readonly ISpanContext EXAMPLE_SPAN_CONTEXT = SpanContext.Create(TRACE_ID, SPAN_ID, TRACE_OPTIONS, Tracestate.Empty);

        private readonly BinaryFormat binaryFormat = new BinaryFormat();

        private void TestSpanContextConversion(ISpanContext spanContext)
        {
            ISpanContext propagatedBinarySpanContext = binaryFormat.FromByteArray(binaryFormat.ToByteArray(spanContext));
            Assert.Equal(spanContext, propagatedBinarySpanContext);
        }

        [Fact]
        public void Propagate_SpanContextTracingEnabled()
        {
            TestSpanContextConversion(
            SpanContext.Create(TRACE_ID, SPAN_ID, TraceOptions.Builder().SetIsSampled(true).Build(), Tracestate.Empty));
        }

        [Fact]
        public void Propagate_SpanContextNoTracing()
        {
            TestSpanContextConversion(SpanContext.Create(TRACE_ID, SPAN_ID, TraceOptions.Default, Tracestate.Empty));
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
                binaryFormat.ToByteArray(SpanContext.Invalid));

        }

        [Fact]
        public void FromBinaryValue_BinaryExampleValue()
        {
            Assert.Equal(EXAMPLE_SPAN_CONTEXT, binaryFormat.FromByteArray(EXAMPLE_BYTES));
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
                new byte[] {
          66, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 97, 98, 99, 100, 101,
          102, 103, 104, 1,
                }));
        }

        [Fact]
        public void FromBinaryValue_UnsupportedFieldIdFirst()
        {
            Assert.Equal(
                SpanContext.Create(TraceId.Invalid, SpanId.Invalid, TraceOptions.Default, Tracestate.Empty),
                binaryFormat.FromByteArray(
                        new byte[] {
                  0, 4, 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79, 1, 97, 98,
                  99, 100, 101, 102, 103, 104, 2, 1,
                        }));
        }

        [Fact]
        public void FromBinaryValue_UnsupportedFieldIdSecond()
        {
            Assert.Equal(
                 SpanContext.Create(
                        TraceId.FromBytes(
                            new byte[] { 64, 65, 66, 67, 68, 69, 70, 71, 72, 73, 74, 75, 76, 77, 78, 79 }),
                        SpanId.Invalid,
                        TraceOptions.Default, Tracestate.Empty),
                 binaryFormat.FromByteArray(
                        new byte[] {
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
