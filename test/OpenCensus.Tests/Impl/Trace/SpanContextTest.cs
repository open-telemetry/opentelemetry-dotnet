// <copyright file="SpanContextTest.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Test
{
    using Xunit;

    public class SpanContextTest
    {
        private static readonly byte[] firstTraceIdBytes =
         new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)'a' };

        private static readonly byte[] secondTraceIdBytes =
            new byte[] { 0, 0, 0, 0, 0, 0, 0, (byte)'0', 0, 0, 0, 0, 0, 0, 0, 0 };

        private static readonly byte[] firstSpanIdBytes = new byte[] { 0, 0, 0, 0, 0, 0, 0, (byte)'a' };
        private static readonly byte[] secondSpanIdBytes = new byte[] { (byte)'0', 0, 0, 0, 0, 0, 0, 0 };
        private static readonly ISpanContext first =
      SpanContext.Create(
          TraceId.FromBytes(firstTraceIdBytes),
          SpanId.FromBytes(firstSpanIdBytes),
          TraceOptions.Default, Tracestate.Empty);

        private static readonly ISpanContext second =
      SpanContext.Create(
          TraceId.FromBytes(secondTraceIdBytes),
          SpanId.FromBytes(secondSpanIdBytes),
          TraceOptions.Builder().SetIsSampled(true).Build(), Tracestate.Empty);

        [Fact]
        public void InvalidSpanContext()
        {
            Assert.Equal(TraceId.Invalid, SpanContext.Invalid.TraceId);
            Assert.Equal(SpanId.Invalid, SpanContext.Invalid.SpanId);
            Assert.Equal(TraceOptions.Default, SpanContext.Invalid.TraceOptions);
        }

        [Fact]
        public void IsValid()
        {
            Assert.False(SpanContext.Invalid.IsValid);
            Assert.False(
                    SpanContext.Create(
                            TraceId.FromBytes(firstTraceIdBytes), SpanId.Invalid, TraceOptions.Default, Tracestate.Empty)
                        .IsValid);
            Assert.False(
                    SpanContext.Create(
                            TraceId.Invalid, SpanId.FromBytes(firstSpanIdBytes), TraceOptions.Default, Tracestate.Empty)
                        .IsValid);
            Assert.True(first.IsValid);
            Assert.True(second.IsValid);
        }

        [Fact]
        public void GetTraceId()
        {
            Assert.Equal(TraceId.FromBytes(firstTraceIdBytes), first.TraceId);
            Assert.Equal(TraceId.FromBytes(secondTraceIdBytes), second.TraceId);
        }

        [Fact]
        public void GetSpanId()
        {
            Assert.Equal(SpanId.FromBytes(firstSpanIdBytes), first.SpanId);
            Assert.Equal(SpanId.FromBytes(secondSpanIdBytes), second.SpanId);
        }

        [Fact]
        public void GetTraceOptions()
        {
            Assert.Equal(TraceOptions.Default, first.TraceOptions);
            Assert.Equal(TraceOptions.Builder().SetIsSampled(true).Build(), second.TraceOptions);
        }

        [Fact]
        public void SpanContext_EqualsAndHashCode()
        {
            // EqualsTester tester = new EqualsTester();
            // tester.addEqualityGroup(
            //    first,
            //    SpanContext.create(
            //        TraceId.FromBytes(firstTraceIdBytes),
            //        SpanId.FromBytes(firstSpanIdBytes),
            //        TraceOptions.DEFAULT),
            //    SpanContext.create(
            //        TraceId.FromBytes(firstTraceIdBytes),
            //        SpanId.FromBytes(firstSpanIdBytes),
            //        TraceOptions.builder().setIsSampled(false).build()));
            // tester.addEqualityGroup(
            //    second,
            //    SpanContext.create(
            //        TraceId.FromBytes(secondTraceIdBytes),
            //        SpanId.FromBytes(secondSpanIdBytes),
            //        TraceOptions.builder().setIsSampled(true).build()));
            // tester.testEquals();
        }

        [Fact]
        public void SpanContext_ToString()
        {
            Assert.Contains(TraceId.FromBytes(firstTraceIdBytes).ToString(), first.ToString());
            Assert.Contains(SpanId.FromBytes(firstSpanIdBytes).ToString(), first.ToString());
            Assert.Contains(TraceOptions.Default.ToString(), first.ToString());
            Assert.Contains(TraceId.FromBytes(secondTraceIdBytes).ToString(), second.ToString());
            Assert.Contains(SpanId.FromBytes(secondSpanIdBytes).ToString(), second.ToString());
            Assert.Contains(TraceOptions.Builder().SetIsSampled(true).Build().ToString(), second.ToString());
        }
    }
}
