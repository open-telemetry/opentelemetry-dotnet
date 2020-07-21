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
        private const string ActivityName1 = "MyActivityName/1";
        private const string ActivitySourceName = "defaultactivitysource";

        private ActivitySource activitySource = default;

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
            this.activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ScopeManagerShim(this.activitySource);

            Assert.Null(Activity.Current);
            Assert.Null(shim.Active);
        }

        [Fact]
        public void Active_IsNotNull()
        {
            // var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            this.activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ScopeManagerShim(this.activitySource);
            var openTracingSpan = new ActivityShim(this.activitySource.StartActivity(ActivityName1));

            var scope = shim.Activate(openTracingSpan, true);
            Assert.NotNull(scope);

            var activeScope = shim.Active;
            Assert.Equal(scope, activeScope);
            openTracingSpan.Finish();
        }

        [Fact]
        public void Activate_SpanMustBeShim()
        {
            this.activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ScopeManagerShim(this.activitySource);

            Assert.Throws<ArgumentException>(() => shim.Activate(new Mock<global::OpenTracing.ISpan>().Object, true));
        }

        [Fact]
        public void Activate()
        {
            this.activitySource = new ActivitySource(ActivitySourceName);
            var shim = new ScopeManagerShim(this.activitySource);
            var spanShim = new ActivityShim(this.activitySource.StartActivity(ActivityName1));

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
            Assert.NotEqual(default, spanShim.activity.Duration);
        }
    }
}
