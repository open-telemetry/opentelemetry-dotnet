// <copyright file="ScopeManagerShimTests.cs" company="OpenTelemetry Authors">
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

using System;
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    public class ScopeManagerShimTests
    {
        [Fact]
        public void CtorArgumentValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new ScopeManagerShim(null));
        }

        [Fact]
        public void Active_IsNull()
        {
            var tracer = TracerFactoryBase.Default.GetTracer(null);
            var shim = new ScopeManagerShim(tracer);

            Assert.False(tracer.CurrentSpan.Context.IsValid);
            Assert.Null(shim.Active);
        }

        [Fact]
        public void Active_IsNotNull()
        {
            var tracerMock = new Mock<Tracer>();
            var shim = new ScopeManagerShim(tracerMock.Object);
            var openTracingSpan = new SpanShim(Defaults.GetOpenTelemetrySpanMock());
            var scopeMock = new Mock<IDisposable>();

            tracerMock.Setup(x => x.WithSpan(openTracingSpan.Span, It.IsAny<bool>())).Returns(scopeMock.Object);
            tracerMock.Setup(x => x.CurrentSpan).Returns(openTracingSpan.Span);

            var scope = shim.Activate(openTracingSpan, true);
            Assert.NotNull(scope);

            var activeScope = shim.Active;
            Assert.Equal(scope, activeScope);
        }

        [Fact]
        public void Activate_SpanMustBeShim()
        {
            var tracerMock = new Mock<Tracer>();
            var shim = new ScopeManagerShim(tracerMock.Object);

            Assert.Throws<ArgumentException>(() => shim.Activate(new Mock<global::OpenTracing.ISpan>().Object, true));
        }

        [Fact]
        public void Activate()
        {
            var tracerMock = new Mock<Tracer>();
            var shim = new ScopeManagerShim(tracerMock.Object);
            var scopeMock = new Mock<IDisposable>();
            var spanShim = new SpanShim(Defaults.GetOpenTelemetryMockSpan().Object);

            tracerMock.Setup(x => x.WithSpan(spanShim.Span, It.IsAny<bool>())).Returns(scopeMock.Object);

            using (shim.Activate(spanShim, true))
            {
#if DEBUG
                Assert.Equal(1, shim.SpanScopeTableCount);
#endif
            }

#if DEBUG
            Assert.Equal(0, shim.SpanScopeTableCount);
#endif
            tracerMock.Verify(x => x.WithSpan(spanShim.Span, It.IsAny<bool>()), Times.Once);
            scopeMock.Verify(x => x.Dispose(), Times.Once);
        }
    }
}
