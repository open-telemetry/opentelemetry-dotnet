// <copyright file="SpanContextTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Test
{
    using System.Diagnostics;
    using Xunit;

    public class SpanContextTest
    {
        private static readonly byte[] firstTraceIdBytes =
         new byte[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)'a' };

        private static readonly byte[] secondTraceIdBytes =
            new byte[] { 0, 0, 0, 0, 0, 0, 0, (byte)'0', 0, 0, 0, 0, 0, 0, 0, 0 };

        private static readonly byte[] firstSpanIdBytes = new byte[] { 0, 0, 0, 0, 0, 0, 0, (byte)'a' };
        private static readonly byte[] secondSpanIdBytes = new byte[] { (byte)'0', 0, 0, 0, 0, 0, 0, 0 };
        private static readonly SpanContext first =
      SpanContext.Create(
          ActivityTraceId.CreateFromBytes(firstTraceIdBytes),
          ActivitySpanId.CreateFromBytes(firstSpanIdBytes),
          ActivityTraceFlags.None, Tracestate.Empty);

        private static readonly SpanContext second =
      SpanContext.Create(
          ActivityTraceId.CreateFromBytes(secondTraceIdBytes),
          ActivitySpanId.CreateFromBytes(secondSpanIdBytes),
          ActivityTraceFlags.Recorded, Tracestate.Empty);

        [Fact]
        public void InvalidSpanContext()
        {
            Assert.Equal(default, SpanContext.Blank.TraceId);
            Assert.Equal(default, SpanContext.Blank.SpanId);
            Assert.Equal(ActivityTraceFlags.None, SpanContext.Blank.TraceOptions);
        }

        [Fact]
        public void IsValid()
        {
            Assert.False(SpanContext.Blank.IsValid);
            Assert.False(
                    SpanContext.Create(
                            ActivityTraceId.CreateFromBytes(firstTraceIdBytes), default, ActivityTraceFlags.None, Tracestate.Empty)
                        .IsValid);
            Assert.False(
                    SpanContext.Create(
                            default, ActivitySpanId.CreateFromBytes(firstSpanIdBytes), ActivityTraceFlags.None, Tracestate.Empty)
                        .IsValid);
            Assert.True(first.IsValid);
            Assert.True(second.IsValid);
        }

        [Fact]
        public void GetTraceId()
        {
            Assert.Equal(ActivityTraceId.CreateFromBytes(firstTraceIdBytes), first.TraceId);
            Assert.Equal(ActivityTraceId.CreateFromBytes(secondTraceIdBytes), second.TraceId);
        }

        [Fact]
        public void GetSpanId()
        {
            Assert.Equal(ActivitySpanId.CreateFromBytes(firstSpanIdBytes), first.SpanId);
            Assert.Equal(ActivitySpanId.CreateFromBytes(secondSpanIdBytes), second.SpanId);
        }

        [Fact]
        public void GetTraceOptions()
        {
            Assert.Equal(ActivityTraceFlags.None, first.TraceOptions);
            Assert.Equal(ActivityTraceFlags.Recorded, second.TraceOptions);
        }

        [Fact]
        public void SpanContext_EqualsAndHashCode()
        {
            // EqualsTester tester = new EqualsTester();
            // tester.addEqualityGroup(
            //    first,
            //    SpanContext.create(
            //        ActivityTraceId.CreateFromBytes(firstTraceIdBytes),
            //        ActivitySpanId.CreateFromBytes(firstSpanIdBytes),
            //        TraceOptions.DEFAULT),
            //    SpanContext.create(
            //        ActivityTraceId.CreateFromBytes(firstTraceIdBytes),
            //        ActivitySpanId.CreateFromBytes(firstSpanIdBytes),
            //        TraceOptions.builder().setIsSampled(false).build()));
            // tester.addEqualityGroup(
            //    second,
            //    SpanContext.create(
            //        ActivityTraceId.CreateFromBytes(secondTraceIdBytes),
            //        ActivitySpanId.CreateFromBytes(secondSpanIdBytes),
            //        TraceOptions.builder().setIsSampled(true).build()));
            // tester.testEquals();
        }

        [Fact]
        public void SpanContext_ToString()
        {
            Assert.Contains(ActivityTraceId.CreateFromBytes(firstTraceIdBytes).ToString(), first.ToString());
            Assert.Contains(ActivitySpanId.CreateFromBytes(firstSpanIdBytes).ToString(), first.ToString());
            Assert.Contains(ActivityTraceFlags.None.ToString(), first.ToString());
            Assert.Contains(ActivityTraceId.CreateFromBytes(secondTraceIdBytes).ToString(), second.ToString());
            Assert.Contains(ActivitySpanId.CreateFromBytes(secondSpanIdBytes).ToString(), second.ToString());
            Assert.Contains(ActivityTraceFlags.Recorded.ToString(), second.ToString());
        }
    }
}
