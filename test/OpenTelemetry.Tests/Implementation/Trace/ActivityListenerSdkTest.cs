// <copyright file="ActivityListenerSdkTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Configuration;
using Xunit;

namespace OpenTelemetry.Tests.Implementation.Trace
{
    public class ActivityListenerSdkTest
    {
        static ActivityListenerSdkTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;
        }

        [Fact]
        public void BuildSamplingParametersHandlesCurrentActivity()
        {
            using var activitySource = new ActivitySource(nameof(BuildSamplingParametersHandlesCurrentActivity));

            var latestSamplingParameters = new ActivitySamplingParameters();

            using var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) =>
                {
                    OpenTelemetrySdk.BuildSamplingParameters(options, out latestSamplingParameters);
                    return ActivityDataRequest.AllDataAndRecorded;
                },
            };

            ActivitySource.AddActivityListener(listener);

            using (var root = activitySource.StartActivity("root"))
            {
                Assert.Equal(default(ActivitySpanId), root.ParentSpanId);

                // This enforces the current behavior that the traceId passed to the sampler for the
                // root span/activity is not the traceId actually used.
                Assert.NotEqual(root.TraceId, latestSamplingParameters.TraceId);
            }

            using (var parent = activitySource.StartActivity("parent", ActivityKind.Client))
            {
                // This enforces the current behavior that the traceId passed to the sampler for the
                // root span/activity is not the traceId actually used.
                Assert.NotEqual(parent.TraceId, latestSamplingParameters.TraceId);
                using (var child = activitySource.StartActivity("child"))
                {
                    Assert.Equal(parent.TraceId, latestSamplingParameters.TraceId);
                    Assert.Equal(parent.TraceId, child.TraceId);
                    Assert.Equal(parent.SpanId, child.ParentSpanId);
                }
            }

            var customContext = new ActivityContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.None);
            using (var fromCustomContext =
                activitySource.StartActivity("customContext", ActivityKind.Client, customContext))
            {
                Assert.Equal(customContext.TraceId, fromCustomContext.TraceId);
                Assert.Equal(customContext.SpanId, fromCustomContext.ParentSpanId);
                Assert.NotEqual(customContext.SpanId, fromCustomContext.SpanId);
            }
        }
    }
}
