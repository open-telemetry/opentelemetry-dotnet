// <copyright file="TracerProviderSdkTest.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenTelemetry.Instrumentation;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class TracerProviderSdkTest : IDisposable
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
                Assert.True(rootActivity.ParentSpanId == default);

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

        [Theory]
        [InlineData(SamplingDecision.Drop)]
        [InlineData(SamplingDecision.RecordOnly)]
        [InlineData(SamplingDecision.RecordAndSample)]
        public void TracerProviderSdkSamplerAttributesAreAppliedToActivity(SamplingDecision sampling)
        {
            var testSampler = new TestSampler();
            testSampler.SamplingAction = (samplingParams) =>
            {
                var attributes = new Dictionary<string, object>();
                attributes.Add("tagkeybysampler", "tagvalueaddedbysampler");
                return new SamplingResult(sampling, attributes);
            };

            using var activitySource = new ActivitySource(ActivitySourceName);
            using var sdk = Sdk.CreateTracerProviderBuilder()
                .AddSource(ActivitySourceName)
                .SetSampler(testSampler)
                .Build();

            using (var rootActivity = activitySource.StartActivity("root"))
            {
                Assert.NotNull(rootActivity);
                Assert.Equal(rootActivity.TraceId, testSampler.LatestSamplingParameters.TraceId);
                if (sampling != SamplingDecision.Drop)
                {
                    Assert.Contains(new KeyValuePair<string, object>("tagkeybysampler", "tagvalueaddedbysampler"), rootActivity.TagObjects);
                }
            }
        }

        [Fact]
        public void TracerSdkSetsActivitySamplingResultBasedOnSamplingDecision()
        {
            var testSampler = new TestSampler();
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var sdk = Sdk.CreateTracerProviderBuilder()
                    .AddSource(ActivitySourceName)
                    .SetSampler(testSampler)
                    .Build();

            testSampler.SamplingAction = (samplingParameters) =>
            {
                return new SamplingResult(SamplingDecision.RecordAndSample);
            };

            using (var activity = activitySource.StartActivity("root"))
            {
                Assert.NotNull(activity);
                Assert.True(activity.IsAllDataRequested);
                Assert.True(activity.Recorded);
            }

            testSampler.SamplingAction = (samplingParameters) =>
            {
                return new SamplingResult(SamplingDecision.RecordOnly);
            };

            using (var activity = activitySource.StartActivity("root"))
            {
                // Even if sampling returns false, for root activities,
                // activity is still created with PropagationOnly.
                Assert.NotNull(activity);
                Assert.True(activity.IsAllDataRequested);
                Assert.False(activity.Recorded);
            }

            testSampler.SamplingAction = (samplingParameters) =>
            {
                return new SamplingResult(SamplingDecision.Drop);
            };

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
        public void TracerSdkSetsActivitySamplingResultToNoneWhenSuppressInstrumentationIsTrue()
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

            testSampler.SamplingAction = (samplingParameters) =>
            {
                return new SamplingResult(SamplingDecision.Drop);
            };

            using ActivitySource source = new ActivitySource("random");
            var activity = source.StartActivity("somename");
            activity.Stop();

            Assert.False(activity.IsAllDataRequested);
            Assert.Equal(ActivityTraceFlags.None, activity.ActivityTraceFlags);
            Assert.False(activity.Recorded);
            Assert.False(startCalled);
            Assert.False(endCalled);
        }

        // Test to check that TracerProvider does not call Processor.Start or Processor.End for a legacy activity when no legacy OperationName is
        // provided to TracerProviderBuilder.
        [Fact]
        public void SdkDoesNotProcessLegacyActivityWithNoAdditionalConfig()
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

            // No AddLegacyOperationName chained to TracerProviderBuilder
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddProcessor(testActivityProcessor)
                        .Build();

            Activity activity = new Activity("Test");
            activity.Start();
            activity.Stop();

            Assert.False(startCalled);
            Assert.False(endCalled);
        }

        // Test to check that TracerProvider does not call Processor.End for a legacy activity which did not have its ActivitySource updated before Activity.Stop
        [Fact]
        public void SdkProcessLegacyActivityWithNoUpdateToActivitySource()
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

            var operationNameForLegacyActivity = "TestOperationName";

            // Only AddLegacyActivityOperationName is chained which ensures that a legacy activity with empty ActivitySource is sent to Processor.OnStart.
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddLegacyActivityOperationName(operationNameForLegacyActivity)
                        .AddProcessor(testActivityProcessor)
                        .Build();

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();

            // The legacy activity's ActivitySource is not set before activity.Stop. The activity will not be sent to Processor.OnEnd.
            activity.Stop();

            Assert.True(startCalled);
            Assert.False(endCalled);
        }

        // Test to check that TracerProvider does not call Processor.End for a legacy activity which had its ActivitySource updated but the
        // updated ActivitySource was not added to the TracerProvider
        [Fact]
        public void SdkProcessLegacyActivityWithPartialConfig()
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

            var activitySourceForLegacyActvity = new ActivitySource("TestActivitySource", "1.0.0");
            var operationNameForLegacyActivity = "TestOperationName";

            // Only AddLegacyActivityOperationName is chained which ensures that a legacy activity with empty ActivitySource is sent to Processor.OnStart.
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddLegacyActivityOperationName(operationNameForLegacyActivity)
                        .AddProcessor(testActivityProcessor)
                        .Build();

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();

            // The legacy activity's ActivitySource is not set before activity.Stop. The activity should not be sent to Processor.OnEnd.
            activity.Stop();

            Assert.True(startCalled);
            Assert.False(endCalled);
        }

        // Test to check that TracerProvider processes legacy activities when a legacy OperationName and the required Source is provided to the TracerProvider
        [Fact]
        public void SdkProcessesLegacyActivityWithRightConfig()
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

            var activitySourceForLegacyActvity = new ActivitySource("TestActivitySource", "1.0.0");
            var operationNameForLegacyActivity = "TestOperationName";

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddSource(activitySourceForLegacyActvity.Name)
                        .AddLegacyActivityOperationName(operationNameForLegacyActivity)
                        .AddProcessor(testActivityProcessor)
                        .Build();

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();

            // Set legacy activity's ActvitySource before Activity.Stop
            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, activitySourceForLegacyActvity);
            activity.Stop();

            Assert.True(startCalled);
            Assert.True(endCalled);
        }

        // Test to check that TracerProvider continues to process legacy activities even after a new Processor is added after the building the provider.
        [Fact]
        public void SdkProcessesLegacyActivityAfterAddingNewProcessor()
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

            var activitySourceForLegacyActvity = new ActivitySource("TestActivitySource", "1.0.0");
            var operationNameForLegacyActivity = "TestOperationName";

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddSource(activitySourceForLegacyActvity.Name)
                        .AddLegacyActivityOperationName(operationNameForLegacyActivity)
                        .AddProcessor(testActivityProcessor)
                        .Build();

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();

            // Set legacy activity's ActvitySource
            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, activitySourceForLegacyActvity);
            activity.Stop();

            Assert.True(startCalled);
            Assert.True(endCalled);

            // As Processors can be added anytime after Provider construction,
            // the following validates that updated processors are reflected
            // in ActivitySourceAdapter.
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

            Activity activityNew = new Activity(operationNameForLegacyActivity); // Create a new Activity with the same operation name
            activityNew.Start();

            // Set new legacy activity's ActvitySource
            ActivityInstrumentationHelper.SetActivitySourceProperty(activityNew, activitySourceForLegacyActvity);
            activityNew.Stop();

            Assert.True(startCalledNew);
            Assert.True(endCalledNew);
        }

        [Fact]
        public void TracerProvideSdkCreatesAndDiposesInstrumentation()
        {
            TestInstrumentation testInstrumentation = null;
            var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddInstrumentation(() =>
                        {
                            testInstrumentation = new TestInstrumentation();
                            return testInstrumentation;
                        })
                        .Build();

            Assert.NotNull(testInstrumentation);
            Assert.False(testInstrumentation.IsDisposed);
            tracerProvider.Dispose();
            Assert.True(testInstrumentation.IsDisposed);
        }

        [Fact]
        public void TracerProviderSdkBuildsWithDefaultResource()
        {
            var tracerProvider = Sdk.CreateTracerProviderBuilder().Build();
            var resource = tracerProvider.GetResource();
            var attributes = resource.Attributes;

            Assert.NotNull(resource);
            Assert.NotEqual(Resource.Empty, resource);
            Assert.Single(resource.Attributes);
            Assert.Equal(resource.Attributes.FirstOrDefault().Key, ResourceSemanticConventions.AttributeServiceName);
            Assert.Contains("unknown_service", (string)resource.Attributes.FirstOrDefault().Value);
        }

        [Fact]
        public void TracerProviderSdkBuildsWithSDKResource()
        {
            var tracerProvider = Sdk.CreateTracerProviderBuilder().SetResourceBuilder(
                ResourceBuilder.CreateDefault().AddTelemetrySdk()).Build();
            var resource = tracerProvider.GetResource();
            var attributes = resource.Attributes;

            Assert.NotNull(resource);
            Assert.NotEqual(Resource.Empty, resource);
            Assert.Contains(new KeyValuePair<string, object>("telemetry.sdk.name", "opentelemetry"), attributes);
            Assert.Contains(new KeyValuePair<string, object>("telemetry.sdk.language", "dotnet"), attributes);
            var versionAttribute = attributes.Where(pair => pair.Key.Equals("telemetry.sdk.version"));
            Assert.Single(versionAttribute);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }

        private class TestInstrumentation : IDisposable
        {
            public bool IsDisposed;

            public TestInstrumentation()
            {
                this.IsDisposed = false;
            }

            public void Dispose()
            {
                this.IsDisposed = true;
            }
        }
    }
}
