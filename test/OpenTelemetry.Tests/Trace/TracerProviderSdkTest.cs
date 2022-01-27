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

        public TracerProviderSdkTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        }

        [Fact]
        public void TracerProviderSdkInvokesSamplingWithCorrectParameters()
        {
            var testSampler = new TestSampler();
            using var activitySource = new ActivitySource(ActivitySourceName);
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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
                // Verify that StartActivity returns an instance of Activity.
                Assert.NotNull(fromInvalidW3CIdParent);

                // Verify that the TestSampler was invoked and received the correct params.
                Assert.Equal(fromInvalidW3CIdParent.TraceId, testSampler.LatestSamplingParameters.TraceId);

                // OpenTelemetry ActivityContext does not support non W3C Ids.
                Assert.Null(fromInvalidW3CIdParent.ParentId);
                Assert.Equal(default(ActivitySpanId), fromInvalidW3CIdParent.ParentSpanId);
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
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
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
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddSource(ActivitySourceName)
                    .SetSampler(testSampler)
                    .Build();

            using (var activity = activitySource.StartActivity("root"))
            {
                Assert.Null(activity);
            }
        }

        [Fact]
        public void TracerSdkSetsActivityDataRequestedToFalseWhenSuppressInstrumentationIsTrueForLegacyActivity()
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

            using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                        .AddLegacySource("random")
                        .AddProcessor(testActivityProcessor)
                        .SetSampler(new AlwaysOnSampler())
                        .Build();

            using (SuppressInstrumentationScope.Begin(true))
            {
                using var activity = new Activity("random").Start();
                Assert.False(activity.IsAllDataRequested);
            }

            Assert.False(startCalled);
            Assert.False(endCalled);
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

        // Test to check that TracerProvider does not call Processor.OnStart or Processor.OnEnd for a legacy activity when no legacy OperationName is
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
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    endCalled = true;
                };

            var emptyActivitySource = new ActivitySource(string.Empty);
            Assert.False(emptyActivitySource.HasListeners()); // No ActivityListener for empty ActivitySource added yet

            // No AddLegacyOperationName chained to TracerProviderBuilder
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddProcessor(testActivityProcessor)
                        .Build();

            Assert.False(emptyActivitySource.HasListeners()); // No listener for empty ActivitySource even after build

            Activity activity = new Activity("Test");
            activity.Start();
            activity.Stop();

            Assert.False(startCalled); // Processor.OnStart is not called since we did not add any legacy OperationName
            Assert.False(endCalled); // Processor.OnEnd is not called since we did not add any legacy OperationName
        }

        // Test to check that TracerProvider samples a legacy activity using a custom Sampler and calls Processor.OnStart and Processor.OnEnd for the
        // legacy activity when the correct legacy OperationName is provided to TracerProviderBuilder.
        [Fact]
        public void SdkSamplesAndProcessesLegacyActivityWithRightConfig()
        {
            bool samplerCalled = false;

            var sampler = new TestSampler
            {
                SamplingAction =
                (samplingParameters) =>
                {
                    samplerCalled = true;
                    return new SamplingResult(SamplingDecision.RecordAndSample);
                },
            };

            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    Assert.True(samplerCalled);
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    endCalled = true;
                };

            var emptyActivitySource = new ActivitySource(string.Empty);
            Assert.False(emptyActivitySource.HasListeners()); // No ActivityListener for empty ActivitySource added yet

            var operationNameForLegacyActivity = "TestOperationName";

            // AddLegacyOperationName chained to TracerProviderBuilder
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(sampler)
                        .AddProcessor(testActivityProcessor)
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            Assert.True(emptyActivitySource.HasListeners()); // Listener for empty ActivitySource added after TracerProvider build

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();
            activity.Stop();

            Assert.True(startCalled); // Processor.OnStart is called since we added a legacy OperationName
            Assert.True(endCalled); // Processor.OnEnd is called since we added a legacy OperationName
        }

        // Test to check that TracerProvider samples a legacy activity using a custom Sampler and calls Processor.OnStart and Processor.OnEnd for the
        // legacy activity when the correct legacy OperationName is provided to TracerProviderBuilder and a wildcard Source is added
        [Fact]
        public void SdkSamplesAndProcessesLegacyActivityWithRightConfigOnWildCardMode()
        {
            bool samplerCalled = false;

            var sampler = new TestSampler
            {
                SamplingAction =
                (samplingParameters) =>
                {
                    samplerCalled = true;
                    return new SamplingResult(SamplingDecision.RecordAndSample);
                },
            };

            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    Assert.True(samplerCalled);
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    endCalled = true;
                };

            var emptyActivitySource = new ActivitySource(string.Empty);
            Assert.False(emptyActivitySource.HasListeners()); // No ActivityListener for empty ActivitySource added yet

            var operationNameForLegacyActivity = "TestOperationName";

            // AddLegacyOperationName chained to TracerProviderBuilder
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(sampler)
                        .AddSource("ABCCompany.XYZProduct.*") // Adding a wild card source
                        .AddProcessor(testActivityProcessor)
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            Assert.True(emptyActivitySource.HasListeners()); // Listener for empty ActivitySource added after TracerProvider build

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();
            activity.Stop();

            Assert.True(startCalled); // Processor.OnStart is called since we added a legacy OperationName
            Assert.True(endCalled); // Processor.OnEnd is called since we added a legacy OperationName
        }

        // Test to check that TracerProvider does not call Processor.OnEnd for a legacy activity whose ActivitySource got updated before Activity.Stop and
        // the updated source was not added to the Provider
        [Fact]
        public void SdkCallsOnlyProcessorOnStartForLegacyActivityWhenActivitySourceIsUpdatedWithoutAddSource()
        {
            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    endCalled = true;
                };

            var emptyActivitySource = new ActivitySource(string.Empty);
            Assert.False(emptyActivitySource.HasListeners()); // No ActivityListener for empty ActivitySource added yet

            var operationNameForLegacyActivity = "TestOperationName";
            var activitySourceForLegacyActvity = new ActivitySource("TestActivitySource", "1.0.0");

            // AddLegacyOperationName chained to TracerProviderBuilder
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddLegacySource(operationNameForLegacyActivity)
                        .AddProcessor(testActivityProcessor)
                        .Build();

            Assert.True(emptyActivitySource.HasListeners()); // Listener for empty ActivitySource added after TracerProvider build

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();
            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, activitySourceForLegacyActvity);
            activity.Stop();

            Assert.True(startCalled); // Processor.OnStart is called since we provided the legacy OperationName
            Assert.False(endCalled); // Processor.OnEnd is not called since the ActivitySource is updated and the updated source name is not added as a Source to the provider
        }

        // Test to check that TracerProvider calls Processor.OnStart and Processor.OnEnd for a legacy activity whose ActivitySource got updated before Activity.Stop and
        // the updated source was added to the Provider
        [Fact]
        public void SdkProcessesLegacyActivityWhenActivitySourceIsUpdatedWithAddSource()
        {
            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    endCalled = true;
                };

            var emptyActivitySource = new ActivitySource(string.Empty);
            Assert.False(emptyActivitySource.HasListeners()); // No ActivityListener for empty ActivitySource added yet

            var operationNameForLegacyActivity = "TestOperationName";
            var activitySourceForLegacyActvity = new ActivitySource("TestActivitySource", "1.0.0");

            // AddLegacyOperationName chained to TracerProviderBuilder
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddSource(activitySourceForLegacyActvity.Name) // Add the updated ActivitySource as a Source
                        .AddLegacySource(operationNameForLegacyActivity)
                        .AddProcessor(testActivityProcessor)
                        .Build();

            Assert.True(emptyActivitySource.HasListeners()); // Listener for empty ActivitySource added after TracerProvider build

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();
            ActivityInstrumentationHelper.SetActivitySourceProperty(activity, activitySourceForLegacyActvity);
            activity.Stop();

            Assert.True(startCalled); // Processor.OnStart is called since we provided the legacy OperationName
            Assert.True(endCalled); // Processor.OnEnd is not called since the ActivitySource is updated and the updated source name is added as a Source to the provider
        }

        // Test to check that TracerProvider continues to process legacy activities even after a new Processor is added after the building the provider.
        [Fact]
        public void SdkProcessesLegacyActivityEvenAfterAddingNewProcessor()
        {
            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            bool startCalled = false;
            bool endCalled = false;

            testActivityProcessor.StartAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    startCalled = true;
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    endCalled = true;
                };

            var operationNameForLegacyActivity = "TestOperationName";

            // AddLegacyOperationName chained to TracerProviderBuilder
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddProcessor(testActivityProcessor)
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();
            activity.Stop();

            Assert.True(startCalled);
            Assert.True(endCalled);

            // As Processors can be added anytime after Provider construction, the following validates
            // the following validates that updated processors are processing the legacy activities created from here on.
            TestActivityProcessor testActivityProcessorNew = new TestActivityProcessor();

            bool startCalledNew = false;
            bool endCalledNew = false;

            testActivityProcessorNew.StartAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    startCalledNew = true;
                };

            testActivityProcessorNew.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    endCalledNew = true;
                };

            tracerProvider.AddProcessor(testActivityProcessorNew);

            Activity activityNew = new Activity(operationNameForLegacyActivity); // Create a new Activity with the same operation name
            activityNew.Start();
            activityNew.Stop();

            Assert.True(startCalledNew);
            Assert.True(endCalledNew);
        }

        [Fact]
        public void SdkSamplesLegacyActivityWithAlwaysOnSampler()
        {
            var operationNameForLegacyActivity = "TestOperationName";
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(new AlwaysOnSampler())
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();

            Assert.True(activity.IsAllDataRequested);
            Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));

            // Validating ActivityTraceFlags is not enough as it does not get reflected on
            // Id, If the Id is accessed before the sampler runs.
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/2700
            Assert.EndsWith("-01", activity.Id);

            activity.Stop();
        }

        [Fact]
        public void SdkSamplesLegacyActivityWithAlwaysOffSampler()
        {
            var operationNameForLegacyActivity = "TestOperationName";
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(new AlwaysOffSampler())
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();

            Assert.False(activity.IsAllDataRequested);
            Assert.False(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));

            // Validating ActivityTraceFlags is not enough as it does not get reflected on
            // Id, If the Id is accessed before the sampler runs.
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/2700
            Assert.EndsWith("-00", activity.Id);

            activity.Stop();
        }

        [Theory]
        [InlineData(SamplingDecision.Drop, false, false)]
        [InlineData(SamplingDecision.RecordOnly, true, false)]
        [InlineData(SamplingDecision.RecordAndSample, true, true)]
        public void SdkSamplesLegacyActivityWithCustomSampler(SamplingDecision samplingDecision, bool isAllDataRequested, bool hasRecordedFlag)
        {
            var operationNameForLegacyActivity = "TestOperationName";
            var sampler = new TestSampler() { SamplingAction = (samplingParameters) => new SamplingResult(samplingDecision) };

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(sampler)
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();

            Assert.Equal(isAllDataRequested, activity.IsAllDataRequested);
            Assert.Equal(hasRecordedFlag, activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));

            // Validating ActivityTraceFlags is not enough as it does not get reflected on
            // Id, If the Id is accessed before the sampler runs.
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/2700
            Assert.EndsWith(hasRecordedFlag ? "-01" : "-00", activity.Id);

            activity.Stop();
        }

        [Fact]
        public void SdkPopulatesSamplingParamsCorrectlyForRootLegacyActivity()
        {
            var operationNameForLegacyActivity = "TestOperationName";
            var sampler = new TestSampler()
            {
                SamplingAction = (samplingParameters) =>
                {
                    Assert.Equal(default, samplingParameters.ParentContext);
                    return new SamplingResult(SamplingDecision.RecordAndSample);
                },
            };

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(sampler)
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            // Start activity without setting parent. i.e it'll have null parent
            // and becomes root activity
            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();
            activity.Stop();
        }

        [Theory]
        [InlineData(SamplingDecision.Drop, ActivityTraceFlags.None, false, false)]
        [InlineData(SamplingDecision.Drop, ActivityTraceFlags.Recorded, false, false)]
        [InlineData(SamplingDecision.RecordOnly, ActivityTraceFlags.None, true, false)]
        [InlineData(SamplingDecision.RecordOnly, ActivityTraceFlags.Recorded, true, false)]
        [InlineData(SamplingDecision.RecordAndSample, ActivityTraceFlags.None, true, true)]
        [InlineData(SamplingDecision.RecordAndSample, ActivityTraceFlags.Recorded, true, true)]
        public void SdkSamplesLegacyActivityWithRemoteParentWithCustomSampler(SamplingDecision samplingDecision, ActivityTraceFlags parentTraceFlags, bool expectedIsAllDataRequested, bool hasRecordedFlag)
        {
            var parentTraceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            var parentTraceFlag = (parentTraceFlags == ActivityTraceFlags.Recorded) ? "01" : "00";
            string remoteParentId = $"00-{parentTraceId}-{parentSpanId}-{parentTraceFlag}";
            string tracestate = "a=b;c=d";

            var operationNameForLegacyActivity = "TestOperationName";
            var sampler = new TestSampler()
            {
                SamplingAction = (samplingParameters) =>
                {
                    // Ensure that SDK populates the sampling parameters correctly
                    Assert.Equal(parentTraceId, samplingParameters.ParentContext.TraceId);
                    Assert.Equal(parentSpanId, samplingParameters.ParentContext.SpanId);
                    Assert.Equal(parentTraceFlags, samplingParameters.ParentContext.TraceFlags);
                    Assert.Equal(tracestate, samplingParameters.ParentContext.TraceState);
                    return new SamplingResult(samplingDecision);
                },
            };

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(sampler)
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            // Create an activity with remote parent id.
            // The sampling parameters are expected to be that of the
            // parent context i.e the remote parent.

            Activity activity = new Activity(operationNameForLegacyActivity).SetParentId(remoteParentId);
            activity.TraceStateString = tracestate;

            // At this point SetParentId has set the ActivityTraceFlags to that of the parent activity. The activity is now passed to the sampler.
            activity.Start();
            Assert.Equal(expectedIsAllDataRequested, activity.IsAllDataRequested);
            Assert.Equal(hasRecordedFlag, activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));

            // Validating ActivityTraceFlags is not enough as it does not get reflected on
            // Id, If the Id is accessed before the sampler runs.
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/2700
            Assert.EndsWith(hasRecordedFlag ? "-01" : "-00", activity.Id);
            activity.Stop();
        }

        [Theory]
        [InlineData(ActivityTraceFlags.None)]
        [InlineData(ActivityTraceFlags.Recorded)]
        public void SdkSamplesLegacyActivityWithRemoteParentWithAlwaysOnSampler(ActivityTraceFlags parentTraceFlags)
        {
            var parentTraceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            var parentTraceFlag = (parentTraceFlags == ActivityTraceFlags.Recorded) ? "01" : "00";
            string remoteParentId = $"00-{parentTraceId}-{parentSpanId}-{parentTraceFlag}";

            var operationNameForLegacyActivity = "TestOperationName";

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(new AlwaysOnSampler())
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            // Create an activity with remote parent id.
            // The sampling parameters are expected to be that of the
            // parent context i.e the remote parent.

            Activity activity = new Activity(operationNameForLegacyActivity).SetParentId(remoteParentId);

            // At this point SetParentId has set the ActivityTraceFlags to that of the parent activity. The activity is now passed to the sampler.
            activity.Start();
            Assert.True(activity.IsAllDataRequested);
            Assert.True(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));

            // Validating ActivityTraceFlags is not enough as it does not get reflected on
            // Id, If the Id is accessed before the sampler runs.
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/2700
            Assert.EndsWith("-01", activity.Id);
            activity.Stop();
        }

        [Theory]
        [InlineData(ActivityTraceFlags.None)]
        [InlineData(ActivityTraceFlags.Recorded)]
        public void SdkSamplesLegacyActivityWithRemoteParentWithAlwaysOffSampler(ActivityTraceFlags parentTraceFlags)
        {
            var parentTraceId = ActivityTraceId.CreateRandom();
            var parentSpanId = ActivitySpanId.CreateRandom();
            var parentTraceFlag = (parentTraceFlags == ActivityTraceFlags.Recorded) ? "01" : "00";
            string remoteParentId = $"00-{parentTraceId}-{parentSpanId}-{parentTraceFlag}";

            var operationNameForLegacyActivity = "TestOperationName";

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(new AlwaysOffSampler())
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            // Create an activity with remote parent id.
            // The sampling parameters are expected to be that of the
            // parent context i.e the remote parent.

            Activity activity = new Activity(operationNameForLegacyActivity).SetParentId(remoteParentId);

            // At this point SetParentId has set the ActivityTraceFlags to that of the parent activity. The activity is now passed to the sampler.
            activity.Start();
            Assert.False(activity.IsAllDataRequested);
            Assert.False(activity.ActivityTraceFlags.HasFlag(ActivityTraceFlags.Recorded));

            // Validating ActivityTraceFlags is not enough as it does not get reflected on
            // Id, If the Id is accessed before the sampler runs.
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/2700
            Assert.EndsWith("-00", activity.Id);
            activity.Stop();
        }

        [Theory]
        [InlineData(ActivityTraceFlags.None)]
        [InlineData(ActivityTraceFlags.Recorded)]
        public void SdkPopulatesSamplingParamsCorrectlyForLegacyActivityWithInProcParent(ActivityTraceFlags traceFlags)
        {
            // Create some parent activity.
            string tracestate = "a=b;c=d";
            var activityLocalParent = new Activity("TestParent");
            activityLocalParent.ActivityTraceFlags = traceFlags;
            activityLocalParent.TraceStateString = tracestate;
            activityLocalParent.Start();

            var operationNameForLegacyActivity = "TestOperationName";
            var sampler = new TestSampler()
            {
                SamplingAction = (samplingParameters) =>
                {
                    Assert.Equal(activityLocalParent.TraceId, samplingParameters.ParentContext.TraceId);
                    Assert.Equal(activityLocalParent.SpanId, samplingParameters.ParentContext.SpanId);
                    Assert.Equal(activityLocalParent.ActivityTraceFlags, samplingParameters.ParentContext.TraceFlags);
                    Assert.Equal(tracestate, samplingParameters.ParentContext.TraceState);
                    return new SamplingResult(SamplingDecision.RecordAndSample);
                },
            };

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(sampler)
                        .AddLegacySource(operationNameForLegacyActivity)
                        .Build();

            // This activity will have a inproc parent.
            // activity.Parent will be equal to the activity created at the beginning of this test.
            // Sampling parameters are expected to be that of the parentContext.
            // i.e of the parent Activity
            Activity activity = new Activity(operationNameForLegacyActivity);
            activity.Start();
            activity.Stop();
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

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        public void AddLegacyOperationName_BadArgs(string operationName)
        {
            var builder = Sdk.CreateTracerProviderBuilder();
            Assert.Throws<ArgumentException>(() => builder.AddLegacySource(operationName));
        }

        [Fact]
        public void AddLegacyOperationNameAddsActivityListenerForEmptyActivitySource()
        {
            var emptyActivitySource = new ActivitySource(string.Empty);
            var builder = Sdk.CreateTracerProviderBuilder();
            builder.AddLegacySource("TestOperationName");

            Assert.False(emptyActivitySource.HasListeners());
            using var provider = builder.Build();
            Assert.True(emptyActivitySource.HasListeners());
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

        [Fact]
        public void TracerProviderSdkFlushesProcessorForcibly()
        {
            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .AddProcessor(testActivityProcessor)
                        .Build();

            var isFlushed = tracerProvider.ForceFlush();

            Assert.True(isFlushed);
            Assert.True(testActivityProcessor.ForceFlushCalled);
        }

        [Fact]
        public void SdkSamplesAndProcessesLegacySourceWhenAddLegacySourceIsCalledWithWildcardValue()
        {
            var sampledActivities = new List<string>();
            var sampler = new TestSampler
            {
                SamplingAction =
                (samplingParameters) =>
                {
                    sampledActivities.Add(samplingParameters.Name);
                    return new SamplingResult(SamplingDecision.RecordAndSample);
                },
            };

            using TestActivityProcessor testActivityProcessor = new TestActivityProcessor();

            var onStartProcessedActivities = new List<string>();
            var onStopProcessedActivities = new List<string>();
            testActivityProcessor.StartAction =
                (a) =>
                {
                    Assert.Contains(a.OperationName, sampledActivities);
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Proccessor.OnStart is called, activity's IsAllDataRequested is set to true
                    onStartProcessedActivities.Add(a.OperationName);
                };

            testActivityProcessor.EndAction =
                (a) =>
                {
                    Assert.False(Sdk.SuppressInstrumentation);
                    Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                    onStopProcessedActivities.Add(a.OperationName);
                };

            var legacySourceNamespaces = new[] { "LegacyNamespace.*", "Namespace.*.Operation" };
            using var activitySource = new ActivitySource(ActivitySourceName);

            // AddLegacyOperationName chained to TracerProviderBuilder
            using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                        .SetSampler(sampler)
                        .AddProcessor(testActivityProcessor)
                        .AddLegacySource(legacySourceNamespaces[0])
                        .AddLegacySource(legacySourceNamespaces[1])
                        .AddSource(ActivitySourceName)
                        .Build();

            foreach (var ns in legacySourceNamespaces)
            {
                var startOpName = ns.Replace("*", "Start");
                Activity startOperation = new Activity(startOpName);
                startOperation.Start();
                startOperation.Stop();

                Assert.Contains(startOpName, onStartProcessedActivities); // Processor.OnStart is called since we added a legacy OperationName
                Assert.Contains(startOpName, onStopProcessedActivities);  // Processor.OnEnd is called since we added a legacy OperationName

                var stopOpName = ns.Replace("*", "Stop");
                Activity stopOperation = new Activity(stopOpName);
                stopOperation.Start();
                stopOperation.Stop();

                Assert.Contains(stopOpName, onStartProcessedActivities); // Processor.OnStart is called since we added a legacy OperationName
                Assert.Contains(stopOpName, onStopProcessedActivities);  // Processor.OnEnd is called since we added a legacy OperationName
            }

            // Ensure we can still process "normal" activities when in legacy wildcard mode.
            Activity nonLegacyActivity = activitySource.StartActivity("TestActivity");
            nonLegacyActivity.Start();
            nonLegacyActivity.Stop();

            Assert.Contains(nonLegacyActivity.OperationName, onStartProcessedActivities); // Processor.OnStart is called since we added a legacy OperationName
            Assert.Contains(nonLegacyActivity.OperationName, onStopProcessedActivities);  // Processor.OnEnd is called since we added a legacy OperationName
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
