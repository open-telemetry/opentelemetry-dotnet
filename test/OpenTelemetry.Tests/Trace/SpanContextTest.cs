// <copyright file="SpanContextTest.cs" company="OpenTelemetry Authors">
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
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class SpanContextTest
    {
        private static readonly byte[] FirstTraceIdBytes = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)'a' };
        private static readonly byte[] SecondTraceIdBytes = { 0, 0, 0, 0, 0, 0, 0, (byte)'0', 0, 0, 0, 0, 0, 0, 0, 0 };
        private static readonly byte[] FirstSpanIdBytes = { 0, 0, 0, 0, 0, 0, 0, (byte)'a' };
        private static readonly byte[] SecondSpanIdBytes = { (byte)'0', 0, 0, 0, 0, 0, 0, 0 };

        private static readonly SpanContext First =
          new(
              ActivityTraceId.CreateFromBytes(FirstTraceIdBytes),
              ActivitySpanId.CreateFromBytes(FirstSpanIdBytes),
              ActivityTraceFlags.None);

        private static readonly SpanContext Second =
          new(
              ActivityTraceId.CreateFromBytes(SecondTraceIdBytes),
              ActivitySpanId.CreateFromBytes(SecondSpanIdBytes),
              ActivityTraceFlags.Recorded);

        [Fact]
        public void InvalidSpanContext()
        {
            Assert.Equal(default, default(SpanContext).TraceId);
            Assert.Equal(default, default(SpanContext).SpanId);
            Assert.Equal(ActivityTraceFlags.None, default(SpanContext).TraceFlags);
        }

        [Fact]
        public void IsValid()
        {
            Assert.False(default(SpanContext).IsValid);
            Assert.False(
                    new SpanContext(
                            ActivityTraceId.CreateFromBytes(FirstTraceIdBytes), default, ActivityTraceFlags.None)
                        .IsValid);
            Assert.False(
                    new SpanContext(
                            default, ActivitySpanId.CreateFromBytes(FirstSpanIdBytes), ActivityTraceFlags.None)
                        .IsValid);
            Assert.True(First.IsValid);
            Assert.True(Second.IsValid);
        }

        [Fact]
        public void GetTraceId()
        {
            Assert.Equal(ActivityTraceId.CreateFromBytes(FirstTraceIdBytes), First.TraceId);
            Assert.Equal(ActivityTraceId.CreateFromBytes(SecondTraceIdBytes), Second.TraceId);
        }

        [Fact]
        public void GetSpanId()
        {
            Assert.Equal(ActivitySpanId.CreateFromBytes(FirstSpanIdBytes), First.SpanId);
            Assert.Equal(ActivitySpanId.CreateFromBytes(SecondSpanIdBytes), Second.SpanId);
        }

        [Fact]
        public void GetTraceOptions()
        {
            Assert.Equal(ActivityTraceFlags.None, First.TraceFlags);
            Assert.Equal(ActivityTraceFlags.Recorded, Second.TraceFlags);
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
        public void Equality4()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
            var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, false, tracestate);
            object context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.None, false, tracestate);

            Assert.Equal(context1, context2);
            Assert.True(context1.Equals(context2));
        }

        [Fact]
        public void Not_Equality_DifferentTraceId()
        {
            var spanId = ActivitySpanId.CreateRandom();
            var context1 = new SpanContext(ActivityTraceId.CreateRandom(), spanId, ActivityTraceFlags.Recorded);
            var context2 = new SpanContext(ActivityTraceId.CreateRandom(), spanId, ActivityTraceFlags.Recorded);

            Assert.NotEqual(context1, context2);
            Assert.True(context1 != context2);
        }

        [Fact]
        public void Not_Equality_DifferentSpanId()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var context1 = new SpanContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, true);
            var context2 = new SpanContext(traceId, ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, true);

            Assert.NotEqual(context1, context2);
            Assert.True(context1 != context2);
        }

        [Fact]
        public void Not_Equality_DifferentTraceFlags()
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
        public void Not_Equality_DifferentIsRemote()
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
        public void Not_Equality_DifferentTraceState()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            IEnumerable<KeyValuePair<string, string>> tracestate1 = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("k", "v1") };
            IEnumerable<KeyValuePair<string, string>> tracestate2 = new List<KeyValuePair<string, string>>() { new KeyValuePair<string, string>("k", "v2") };
            var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, true, tracestate1);
            var context2 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, true, tracestate2);

            Assert.NotEqual(context1, context2);
            Assert.True(context1 != context2);
        }

        [Fact]
        public void TestGetHashCode()
        {
            var traceId = ActivityTraceId.CreateRandom();
            var spanId = ActivitySpanId.CreateRandom();
            IEnumerable<KeyValuePair<string, string>> tracestate = new List<KeyValuePair<string, string>>();
            var context1 = new SpanContext(traceId, spanId, ActivityTraceFlags.Recorded, true, tracestate);

            Assert.NotEqual(0, context1.GetHashCode());
        }
    }
}
