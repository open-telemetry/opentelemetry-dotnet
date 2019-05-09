// <copyright file="SpanBuilderBaseTest.cs" company="OpenCensus Authors">
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
    using Moq;
    using Internal;
    using OpenCensus.Common;
    using Xunit;

    public class SpanBuilderBaseTest
    {
        private ITracer tracer;
        private Mock<SpanBuilderBase> spanBuilder = new Mock<SpanBuilderBase>(SpanKind.Unspecified);
        private Mock<SpanBase> span = new Mock<SpanBase>();

        public SpanBuilderBaseTest()
        {
            tracer = Tracing.Tracer;
            spanBuilder.Setup((b) => b.StartSpan()).Returns(span.Object);
        }

        [Fact]
        public void StartScopedSpan()
        {
            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
            IScope scope = spanBuilder.Object.StartScopedSpan();
            try
            {
                Assert.Same(span.Object, tracer.CurrentSpan);
            }
            finally
            {
                scope.Dispose();
            }
            span.Verify(s => s.End(EndSpanOptions.Default));
            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }

        [Fact]
        public void StartScopedSpan_WithParam()
        {
            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);

            IScope scope = spanBuilder.Object.StartScopedSpan(out ISpan outSpan);
            try
            {
                Assert.Same(outSpan, tracer.CurrentSpan);
            }
            finally
            {
                scope.Dispose();
            }
            span.Verify(s => s.End(EndSpanOptions.Default));
            Assert.Same(BlankSpan.Instance, tracer.CurrentSpan);
        }
    }
}
