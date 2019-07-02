// <copyright file="CurrentSpanUtilsTest.cs" company="OpenTelemetry Authors">
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
    using Moq;
    using OpenTelemetry.Trace.Internal;
    using Xunit;

    public class CurrentSpanUtilsTest
    {
        private ISpan span;
        private SpanContext spanContext;
        private SpanOptions spanOptions;
        private CurrentSpanUtils currentUtils;
        public CurrentSpanUtilsTest()
        {
            spanContext =
                SpanContext.Create(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded,
                    Tracestate.Empty);

            spanOptions = SpanOptions.RecordEvents;
            currentUtils = new CurrentSpanUtils();
        }

        [Fact]
        public void CurrentSpan_WhenNoContext()
        {
            Assert.Equal(BlankSpan.Instance, currentUtils.CurrentSpan);
        }

        [Fact]
        public void WithSpan_CloseDetaches()
        {
            var mockSpan = new Mock<TestSpan>(spanContext, new Activity("foo").Start(), spanOptions) { CallBase = true };
            span = mockSpan.Object;

            Assert.Same(BlankSpan.Instance, currentUtils.CurrentSpan);
            using (currentUtils.WithSpan(span, false))
            {
                Assert.Same(span, currentUtils.CurrentSpan);
                Assert.Same(span.Activity, Activity.Current);

            }

            Assert.Same(BlankSpan.Instance, currentUtils.CurrentSpan);
            Assert.Null(Activity.Current);
        }
    }
}
