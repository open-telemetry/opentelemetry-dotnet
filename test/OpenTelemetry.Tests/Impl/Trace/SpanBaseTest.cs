// <copyright file="SpanBaseTest.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;

namespace OpenTelemetry.Trace.Test
{
    using System;
    using System.Collections.Generic;
    using Moq;
    using OpenTelemetry.Trace.Internal;
    using Xunit;

    public class SpanBaseTest
    {
        private SpanContext spanContext;
        private SpanContext notSampledSpanContext;
        private SpanOptions spanOptions;

        public SpanBaseTest()
        {
            spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded,
                    Tracestate.Empty);
            notSampledSpanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.None,
                    Tracestate.Empty);
            spanOptions = SpanOptions.RecordEvents;
        }

        [Fact]
        public void NewSpan_WithNullContext()
        {
            Assert.Throws<ArgumentNullException>(() => new TestSpan(null, default(SpanOptions), null));
        }


        [Fact]
        public void GetOptions_WhenNullOptions()
        {
            var span = new TestSpan(notSampledSpanContext, default(SpanOptions), null);
            Assert.Equal(SpanOptions.None, span.Options);
        }

        [Fact]
        public void GetContextAndOptions()
        {
            var span = new TestSpan(spanContext, spanOptions, null);
            Assert.Equal(spanContext, span.Context);
            Assert.Equal(spanOptions, span.Options);
        }

        [Fact]
        public void PutAttributeCallsAddAttributeByDefault()
        {
            var mockSpan = new Mock<TestSpan>(spanContext, spanOptions) { CallBase = true };
            var span = mockSpan.Object;
            IAttributeValue val = AttributeValue<bool>.Create(true);
            span.SetAttribute("MyKey", val);
            span.End();
            mockSpan.Verify((s) => s.SetAttribute(It.Is<string>((arg) => arg == "MyKey"), It.Is<IAttributeValue>((v) => v == val)));
        }

        [Fact]
        public void EndCallsEndWithDefaultOptions()
        {
            var mockSpan = new Mock<TestSpan>(spanContext, spanOptions) { CallBase = true };
            var span = mockSpan.Object;
            span.End();
            mockSpan.Verify((s) => s.End(EndSpanOptions.Default));
        }

        [Fact]
        public void AddEventDefaultImplementation()
        {
            var mockSpan = new Mock<SpanBase>();
            var span = mockSpan.Object;

            var @event = Event.Create("MyEvent");
            span.AddEvent(@event);

            mockSpan.Verify((s) => s.AddEvent(@event));
        }
    }
}
