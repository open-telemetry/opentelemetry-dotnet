// <copyright file="TracerProvideSdkTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class TracerProvideSdkTest : IDisposable
    {
        private const string ActivitySourceName = "TraceSdkTest";

        [Fact]
        public void TracerProviderSdkInvokesSamplingWithCorrectParameters()
        {
            var testSampler = new TestSampler();
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var sdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(testSampler)
                .Build();

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
            using var sdk = Sdk.CreateTracerProviderBuilder()
                    .AddSource(ActivitySourceName)
                    .SetSampler(testSampler)
                    .Build();

            testSampler.DesiredSamplingResult = new SamplingResult(SamplingDecision.RecordAndSampled);
            using (var activity = activitySource.StartActivity("root"))
            {
                Assert.NotNull(activity);
                Assert.True(activity.IsAllDataRequested);
                Assert.True(activity.Recorded);
            }

            testSampler.DesiredSamplingResult = new SamplingResult(SamplingDecision.Record);
            using (var activity = activitySource.StartActivity("root"))
            {
                // Even if sampling returns false, for root activities,
                // activity is still created with PropagationOnly.
                Assert.NotNull(activity);
                Assert.True(activity.IsAllDataRequested);
                Assert.False(activity.Recorded);
            }

            testSampler.DesiredSamplingResult = new SamplingResult(SamplingDecision.NotRecord);
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

        [Fact]
        public void TracerSdkSetsActivityDataRequestToNoneWhenSuppressInstrumentationIsTrue()
        {
            using var scope = SuppressInstrumentationScope.Begin();

            var testSampler = new TestSampler();
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var sdk = Sdk.CreateTracerProviderBuilder()
                    .AddSource(ActivitySourceName)
                    .SetSampler(testSampler)
                    .Build();

            using (var activity = activitySource.StartActivity("root"))
            {
                Assert.Null(activity);
            }
        }

        [Fact]
        public void ProcessorDoesNotReceiveNotRecordDecisionSpan()
        {
            var testSampler = new TestSampler();
            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    endCalled = true;
                };

            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                        .AddSource("random")
                        .AddProcessor(testActivityProcessor)
                        .SetSampler(testSampler)
                        .Build();

            testSampler.DesiredSamplingResult = new SamplingResult(SamplingDecision.NotRecord);
            using ActivitySource source = new ActivitySource("random");
            var activity = source.StartActivity("somename");
            activity.Stop();

            Assert.False(activity.IsAllDataRequested);
            Assert.Equal(ActivityTraceFlags.None, activity.ActivityTraceFlags);
            Assert.False(activity.Recorded);
            Assert.False(startCalled);
            Assert.False(endCalled);
        }

        [Fact]
        public void TracerProvideSdkCreatesActivitySource()
        {
            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    endCalled = true;
                };

            TestInstrumentation testInstrumentation = null;
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddProcessor(testActivityProcessor)
                        .AddInstrumentation((adapter) =>
                        {
                            testInstrumentation = new TestInstrumentation(adapter);
                            return testInstrumentation;
                        })
                        .Build();

            var adapter = testInstrumentation.Adapter;
            Activity activity = new Activity("test");
            activity.Start();
            adapter.Start(activity);
            adapter.Stop(activity);
            activity.Stop();

            Assert.True(startCalled);
            Assert.True(endCalled);

            TestActivityProcessor testActivityProcessorNew = new TestActivityProcessor();

            bool startCalledNew = false;
            bool endCalledNew = false;

            testActivityProcessorNew.StartAction =
                (a) =>
                {
                    startCalledNew = true;
                };

            testActivityProcessorNew.EndAction =
                (a) =>
                {
                    endCalledNew = true;
                };

            tracerProvider.AddProcessor(testActivityProcessorNew);
            Activity activityNew = new Activity("test");
            activityNew.Start();
            adapter.Start(activityNew);
            adapter.Stop(activityNew);
            activityNew.Stop();

            Assert.True(startCalledNew);
            Assert.True(endCalledNew);
        }

        [Fact]
        public void TracerProvideSdkCreatesActivitySourceWhenNoProcessor()
        {
            TestInstrumentation testInstrumentation = null;
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddInstrumentation((adapter) =>
                        {
                            testInstrumentation = new TestInstrumentation(adapter);
                            return testInstrumentation;
                        })
                        .Build();

            var adapter = testInstrumentation.Adapter;
            Activity activity = new Activity("test");
            activity.Start();
            adapter.Start(activity);
            adapter.Stop(activity);
            activity.Stop();

            // No asserts here. Validates that no exception
            // gets thrown when processors are not added,
            // TODO: Refactor to have more proper unit test
            // to target each individual classes.
        }

        [Fact]
        public void TracerProvideSdkCreatesAndDiposesInstrumentation()
        {
            TestInstrumentation testInstrumentation = null;
            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddInstrumentation((adapter) =>
                        {
                            testInstrumentation = new TestInstrumentation(adapter);
                            return testInstrumentation;
                        })
                        .Build();

            Assert.NotNull(testInstrumentation);
            var adapter = testInstrumentation.Adapter;
            Assert.NotNull(adapter);
            Assert.False(testInstrumentation.IsDisposed);
            tracerProvider.Dispose();
            Assert.True(testInstrumentation.IsDisposed);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private class TestSampler : Sampler
        {
            public SamplingResult DesiredSamplingResult { get; set; } = new SamplingResult(SamplingDecision.RecordAndSampled);

            public SamplingParameters LatestSamplingParameters { get; private set; }

            public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
            {
                this.LatestSamplingParameters = samplingParameters;
                return this.DesiredSamplingResult;
            }
        }

        private class TestInstrumentation : IDisposable
        {
            public bool IsDisposed;
            public ActivitySourceAdapter Adapter;

            public TestInstrumentation(ActivitySourceAdapter adapter)
            {
                this.Adapter = adapter;
                this.IsDisposed = false;
            }

            public void Dispose()
            {
                this.IsDisposed = true;
            }
        }
    }
}
