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

using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Test
{
    public class SpanContextTest
    {
        private static readonly byte[] firstTraceIdBytes = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)'a' };

        private static readonly byte[] secondTraceIdBytes = { 0, 0, 0, 0, 0, 0, 0, (byte)'0', 0, 0, 0, 0, 0, 0, 0, 0 };

        private static readonly byte[] firstSpanIdBytes = { 0, 0, 0, 0, 0, 0, 0, (byte)'a' };
        private static readonly byte[] secondSpanIdBytes = { (byte)'0', 0, 0, 0, 0, 0, 0, 0 };
        private static readonly SpanContext first =
          new SpanContext(
              ActivityTraceId.CreateFromBytes(firstTraceIdBytes),
              ActivitySpanId.CreateFromBytes(firstSpanIdBytes),
              ActivityTraceFlags.None);

        private static readonly SpanContext second =
          new SpanContext(
              ActivityTraceId.CreateFromBytes(secondTraceIdBytes),
              ActivitySpanId.CreateFromBytes(secondSpanIdBytes),
              ActivityTraceFlags.Recorded);

        [Fact]
        public void InvalidSpanContext()
        {
            Assert.Equal(default, default(SpanContext).TraceId);
            Assert.Equal(default, default(SpanContext).SpanId);
            Assert.Equal(ActivityTraceFlags.None, default(SpanContext).TraceOptions);
        }

        [Fact]
        public void IsValid()
        {
            Assert.False(default(SpanContext).IsValid);
            Assert.False(
                    new SpanContext(
                            ActivityTraceId.CreateFromBytes(firstTraceIdBytes), default, ActivityTraceFlags.None)
                        .IsValid);
            Assert.False(
                    new SpanContext(
                            default, ActivitySpanId.CreateFromBytes(firstSpanIdBytes), ActivityTraceFlags.None)
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
        public void Equality1()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded);
            var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded);

            Assert.Equal(context1, context2);
            Assert.True(context1 == context2);
        }

        [Fact]
        public void Equality2()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, true);
            var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, true);

            Assert.Equal(context1, context2);
            Assert.True(context1 == context2);
        }

        [Fact]
        public void Equality3()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
            var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, false, tracestate);
            var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, false, tracestate);

            Assert.Equal(context1, context2);
            Assert.True(context1 == context2);
        }

        [Fact]
        public void Not_Equality1()
        {
            var spanId = ActivitySpanId.CreateRandom();
            var context1 = new SpanContext(ActivityTraceId.CreateRandom(), spanId, ActivityTraceFlags.Recorded);
            var context2 = new SpanContext(ActivityTraceId.CreateRandom(), spanId, ActivityTraceFlags.Recorded);

            Assert.NotEqual(context1, context2);
            Assert.True(context1 != context2);
        }

        [Fact]
        public void Not_Equality2()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var context1 = new SpanContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, true);
            var context2 = new SpanContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, true);

            Assert.NotEqual(context1, context2);
            Assert.True(context1 != context2);
        }

        [Fact]
        public void Not_Equality3()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
            var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, false, tracestate);
            var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, false, tracestate);

            Assert.NotEqual(context1, context2);
            Assert.True(context1 != context2);
        }

        [Fact]
        public void Not_Equality4()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
            var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, true, tracestate);
            var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, false, tracestate);

            Assert.NotEqual(context1, context2);
            Assert.True(context1 != context2);
        }

        [Fact]
        public void Not_Equality5()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
            var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, true, null);
            var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, false, tracestate);

            Assert.NotEqual(context1, context2);
            Assert.True(context1 != context2);
        }
    }
}
