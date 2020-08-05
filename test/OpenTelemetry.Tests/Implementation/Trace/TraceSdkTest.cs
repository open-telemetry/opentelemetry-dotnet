// <copyright file="TraceSdkTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Tests.Implementation.Trace
{
    public class TraceSdkTest
    {
        private const string ActivitySourceName = "TraceSdkTest";

        [Fact]
        public void TracerSdkInvokesSamplingWithCorrectParameters()
        {
            var testSampler = new TestSampler();
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var sdk = Sdk.CreateTracerProvider(
                (tpbuilder) =>
                    {
                        tpbuilder.AddActivitySource(ActivitySourceName);
                        tpbuilder.SetSampler(testSampler);
                    });

            // OpenTelemetry Sdk is expected to set default to W3C.
            Assert.True(Activity.DefaultIdFormat == ActivityIdFormat.W3C);

            using (var rootActivity = activitySource.StartActivity("root"))
            {
                Assert.NotNull(rootActivity);

                // TODO: Follow up with .NET on why ParentSpanId is != default here.
                // Assert.True(rootActivity.ParentSpanId == default);

                // Validate that the TraceId seen by Sampler is same as the
                // Activity when it got created.
                Assert.Equal(rootActivity.TraceId, testSampler.LatestSamplingParameters.TraceId);
            }

            using (var parent = activitySource.StartActivity("parent", ActivityKind.Client))
            {
                Assert.Equal(parent.TraceId, testSampler.LatestSamplingParameters.TraceId);
                using (var child = activitySource.StartActivity("child"))
                {
                    Assert.Equal(child.TraceId, testSampler.LatestSamplingParameters.TraceId);
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
                Assert.Equal(fromCustomContext.TraceId, testSampler.LatestSamplingParameters.TraceId);
                Assert.Equal(customContext.TraceId, fromCustomContext.TraceId);
                Assert.Equal(customContext.SpanId, fromCustomContext.ParentSpanId);
                Assert.NotEqual(customContext.SpanId, fromCustomContext.SpanId);
            }

            // Validate that when StartActivity is called using Parent as string,
            // Sampling is called correctly.
            var act = new Activity("anything").Start();
            act.Stop();
            var customContextAsString = act.Id;
            var expectedTraceId = act.TraceId;
            var expectedParentSpanId = act.SpanId;

            using (var fromCustomContextAsString =
                activitySource.StartActivity("customContext", ActivityKind.Client, customContextAsString))
            {
                Assert.Equal(fromCustomContextAsString.TraceId, testSampler.LatestSamplingParameters.TraceId);
                Assert.Equal(expectedTraceId, fromCustomContextAsString.TraceId);
                Assert.Equal(expectedParentSpanId, fromCustomContextAsString.ParentSpanId);
            }

            using (var fromInvalidW3CIdParent =
                activitySource.StartActivity("customContext", ActivityKind.Client, "InvalidW3CIdParent"))
            {
                // OpenTelemetry ActivityContext does not support
                // non W3C Ids. Starting activity with non W3C Ids
                // will result in no activity being created.
                Assert.Null(fromInvalidW3CIdParent);
            }
        }

        [Fact]
        public void TracerSdkSetsActivityDataRequestBasedOnSamplingDecision()
        {
            var testSampler = new TestSampler();
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var sdk = Sdk.CreateTracerProvider(
                (tpbuilder) =>
                {
                    tpbuilder.AddActivitySource(ActivitySourceName);
                    tpbuilder.SetSampler(testSampler);
                });

            testSampler.DesiredSamplingResult = new SamplingResult(true);
            using (var activity = activitySource.StartActivity("root"))
            {
                Assert.NotNull(activity);
                Assert.True(activity.IsAllDataRequested);
                Assert.True(activity.Recorded);
            }

            testSampler.DesiredSamplingResult = new SamplingResult(false);
            using (var activity = activitySource.StartActivity("root"))
            {
                // Even if sampling returns false, for root activities,
                // activity is still created with PropagationOnly.
                Assert.NotNull(activity);
                Assert.False(activity.IsAllDataRequested);
                Assert.False(activity.Recorded);

                using (var innerActivity = activitySource.StartActivity("inner"))
                {
                    // This is not a root activity.
                    // If sampling returns false, no activity is created at all.
                    Assert.Null(innerActivity);
                }
            }
        }

        private class TestSampler : Sampler
        {
            public SamplingResult DesiredSamplingResult { get; set; } = new SamplingResult(true);

            public SamplingParameters LatestSamplingParameters { get; private set; }

            public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            {
                this.LatestSamplingParameters = samplingParameters;
                return this.DesiredSamplingResult;
            }
        }
    }
}
