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
using System.Diagnostics;
using Moq;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests
{
    public class ScopeManagerShimTests
    {
        private const string SpanName = "MySpanName/1";
        private const string TracerName = "defaultactivitysource";

        static ScopeManagerShimTests()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public void CtorArgumentValidation()
        {
            Assert.Throws<ArgumentNullException>(() => new ScopeManagerShim(null));
        }

        [Fact]
        public void Active_IsNull()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new ScopeManagerShim(tracer);

            Assert.Null(Activity.Current);
            Assert.Null(shim.Active);
        }

        [Fact]
        public void Active_IsNotNull()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new ScopeManagerShim(tracer);
            var openTracingSpan = new SpanShim(tracer.StartSpan(SpanName));

            var scope = shim.Activate(openTracingSpan, true);
            Assert.NotNull(scope);

            var activeScope = shim.Active;
            Assert.Equal(scope.Span.Context.SpanId, activeScope.Span.Context.SpanId);
            openTracingSpan.Finish();
        }

        [Fact]
        public void Activate_SpanMustBeShim()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new ScopeManagerShim(tracer);

            Assert.Throws<ArgumentException>(() => shim.Activate(new Mock<global::OpenTracing.ISpan>().Object, true));
        }

        [Fact]
        public void Activate()
        {
            var tracer = TracerProvider.GetTracer(TracerName);
            var shim = new ScopeManagerShim(tracer);
            var spanShim = new SpanShim(tracer.StartSpan(SpanName));

            using (shim.Activate(spanShim, true))
            {
#if DEBUG
                Assert.Equal(1, shim.SpanScopeTableCount);
#endif
            }

#if DEBUG
            Assert.Equal(0, shim.SpanScopeTableCount);
#endif

            spanShim.Finish();
            Assert.NotEqual(default, spanShim.Span.Activity.Duration);
        }
    }
}
