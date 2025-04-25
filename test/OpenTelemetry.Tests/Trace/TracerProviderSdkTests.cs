// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Resources;
using OpenTelemetry.Resources.Tests;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public sealed class TracerProviderSdkTests : IDisposable
{
    private static readonly Action<Activity, ActivitySource> SetActivitySourceProperty = CreateActivitySourceSetter();

    public TracerProviderSdkTests()
    {
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
    }

    [Fact]
    public void TracerProviderSdkAddSource()
    {
        using var source1 = new ActivitySource($"{Utils.GetCurrentMethodName()}.1");
        using var source2 = new ActivitySource($"{Utils.GetCurrentMethodName()}.2");

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(source1.Name)
            .Build();

        using (var activity = source1.StartActivity("test"))
        {
            Assert.NotNull(activity);
        }

        using (var activity = source2.StartActivity("test"))
        {
            Assert.Null(activity);
        }
    }

    [Fact]
    public void TracerProviderSdkAddSourceWithWildcards()
    {
        using var source1 = new ActivitySource($"{Utils.GetCurrentMethodName()}.A");
        using var source2 = new ActivitySource($"{Utils.GetCurrentMethodName()}.Ab");
        using var source3 = new ActivitySource($"{Utils.GetCurrentMethodName()}.Abc");
        using var source4 = new ActivitySource($"{Utils.GetCurrentMethodName()}.B");

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource($"{Utils.GetCurrentMethodName()}.*")
            .Build())
        {
            using (var activity = source1.StartActivity("test"))
            {
                Assert.NotNull(activity);
            }

            using (var activity = source2.StartActivity("test"))
            {
                Assert.NotNull(activity);
            }

            using (var activity = source3.StartActivity("test"))
            {
                Assert.NotNull(activity);
            }

            using (var activity = source4.StartActivity("test"))
            {
                Assert.NotNull(activity);
            }
        }

        using (var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource($"{Utils.GetCurrentMethodName()}.?")
            .Build())
        {
            using (var activity = source1.StartActivity("test"))
            {
                Assert.NotNull(activity);
            }

            using (var activity = source2.StartActivity("test"))
            {
                Assert.Null(activity);
            }

            using (var activity = source3.StartActivity("test"))
            {
                Assert.Null(activity);
            }

            using (var activity = source4.StartActivity("test"))
            {
                Assert.NotNull(activity);
            }
        }
    }

    [Fact]
    public void TracerProviderSdkInvokesSamplingWithCorrectParameters()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        var testSampler = new TestSampler();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
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
            Assert.Null(testSampler.LatestSamplingParameters.Tags);
            Assert.Null(testSampler.LatestSamplingParameters.Links);
        }

        using (var parent = activitySource.StartActivity("parent", ActivityKind.Client))
        {
            Assert.NotNull(parent);
            Assert.Equal(parent.TraceId, testSampler.LatestSamplingParameters.TraceId);
            using var child = activitySource.StartActivity("child");
            Assert.NotNull(child);
            Assert.Equal(child.TraceId, testSampler.LatestSamplingParameters.TraceId);
            Assert.Null(testSampler.LatestSamplingParameters.Tags);
            Assert.Null(testSampler.LatestSamplingParameters.Links);
            Assert.Equal(parent.TraceId, child.TraceId);
            Assert.Equal(parent.SpanId, child.ParentSpanId);
        }

        var customContext = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None);

        using (var fromCustomContext =
            activitySource.StartActivity("customContext", ActivityKind.Client, customContext))
        {
            Assert.NotNull(fromCustomContext);
            Assert.Equal(fromCustomContext.TraceId, testSampler.LatestSamplingParameters.TraceId);
            Assert.Null(testSampler.LatestSamplingParameters.Tags);
            Assert.Null(testSampler.LatestSamplingParameters.Links);
            Assert.Equal(customContext.TraceId, fromCustomContext.TraceId);
            Assert.Equal(customContext.SpanId, fromCustomContext.ParentSpanId);
            Assert.NotEqual(customContext.SpanId, fromCustomContext.SpanId);
        }

        // Validate that Samplers get the tags passed with Activity creation
        var initialTags = new ActivityTagsCollection();
        initialTags["tagA"] = "tagAValue";
        using (var withInitialTags = activitySource.StartActivity("withInitialTags", ActivityKind.Client, default(ActivityContext), initialTags))
        {
            Assert.NotNull(withInitialTags);
            Assert.Equal(withInitialTags.TraceId, testSampler.LatestSamplingParameters.TraceId);
            Assert.Equal(initialTags, testSampler.LatestSamplingParameters.Tags);
        }

        // Validate that Samplers get the links passed with Activity creation
        var links = new List<ActivityLink>();
        var linkContext1 = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var linkContext2 = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
        var link1 = new ActivityLink(linkContext1);
        var link2 = new ActivityLink(linkContext2);
        links.Add(link1);
        links.Add(link2);

        using (var withInitialTags = activitySource.StartActivity("withLinks", ActivityKind.Client, default(ActivityContext), links: links))
        {
            Assert.NotNull(withInitialTags);
            Assert.Equal(withInitialTags.TraceId, testSampler.LatestSamplingParameters.TraceId);
            Assert.Null(testSampler.LatestSamplingParameters.Tags);
            Assert.Equal(links, testSampler.LatestSamplingParameters.Links);
        }

        // Validate that when StartActivity is called using Parent as string,
        // Sampling is called correctly.
        using var act = new Activity("anything").Start();
        act.Stop();
        var customContextAsString = act.Id;
        var expectedTraceId = act.TraceId;
        var expectedParentSpanId = act.SpanId;

        using (var fromCustomContextAsString =
            activitySource.StartActivity("customContext", ActivityKind.Client, customContextAsString))
        {
            Assert.NotNull(fromCustomContextAsString);
            Assert.Equal(fromCustomContextAsString.TraceId, testSampler.LatestSamplingParameters.TraceId);
            Assert.Equal(expectedTraceId, fromCustomContextAsString.TraceId);
            Assert.Equal(expectedParentSpanId, fromCustomContextAsString.ParentSpanId);
        }

        // Verify that StartActivity returns an instance of Activity.
        using var fromInvalidW3CIdParent =
            activitySource.StartActivity("customContext", ActivityKind.Client, "InvalidW3CIdParent");
        Assert.NotNull(fromInvalidW3CIdParent);

        // Verify that the TestSampler was invoked and received the correct params.
        Assert.Equal(fromInvalidW3CIdParent.TraceId, testSampler.LatestSamplingParameters.TraceId);

        // OpenTelemetry ActivityContext does not support non W3C Ids.
        Assert.Null(fromInvalidW3CIdParent.ParentId);
        Assert.Equal(default, fromInvalidW3CIdParent.ParentSpanId);
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void TracerProviderSdkSamplerAttributesAreAppliedToActivity(SamplingDecision sampling)
    {
        var testSampler = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                var attributes = new Dictionary<string, object>
                {
                    { "tagkeybysampler", "tagvalueaddedbysampler" },
                };
                return new SamplingResult(sampling, attributes);
            },
        };

        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(testSampler)
            .Build();

        using var rootActivity = activitySource.StartActivity("root");
        Assert.NotNull(rootActivity);
        Assert.Equal(rootActivity.TraceId, testSampler.LatestSamplingParameters.TraceId);
        if (sampling != SamplingDecision.Drop)
        {
            Assert.Contains(new KeyValuePair<string, object?>("tagkeybysampler", "tagvalueaddedbysampler"), rootActivity.TagObjects);
        }
    }

    [Fact]
    public void TracerSdkSetsActivitySamplingResultAsPropagationWhenParentIsRemote()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        var testSampler = new TestSampler();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySourceName)
                .SetSampler(testSampler)
                .Build();

        testSampler.SamplingAction = (samplingParameters) =>
        {
            return new SamplingResult(SamplingDecision.Drop);
        };

        ActivityContext ctx = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.None, isRemote: true);

        using (var activity = activitySource.StartActivity("root", ActivityKind.Server, ctx))
        {
            // Even if sampling returns false, for activities with remote parent,
            // activity is still created with PropagationOnly.
            Assert.NotNull(activity);
            Assert.False(activity.IsAllDataRequested);
            Assert.False(activity.Recorded);

            // This is not a root activity and parent is not remote.
            // If sampling returns false, no activity is created at all.
            using var innerActivity = activitySource.StartActivity("inner");
            Assert.Null(innerActivity);
        }
    }

    [Fact]
    public void TracerSdkSetsActivitySamplingResultBasedOnSamplingDecision()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        var testSampler = new TestSampler();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySourceName)
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

            // This is not a root activity.
            // If sampling returns false, no activity is created at all.
            using var innerActivity = activitySource.StartActivity("inner");
            Assert.Null(innerActivity);
        }
    }

    [Fact]
    public void TracerSdkSetsActivitySamplingResultToNoneWhenSuppressInstrumentationIsTrue()
    {
        using var scope = SuppressInstrumentationScope.Begin();

        var activitySourceName = Utils.GetCurrentMethodName();
        var testSampler = new TestSampler();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .AddSource(activitySourceName)
                .SetSampler(testSampler)
                .Build();

        using var activity = activitySource.StartActivity("root");
        Assert.Null(activity);
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

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();

        using var openTelemetry = Sdk.CreateTracerProviderBuilder()
                    .AddLegacySource(operationNameForLegacyActivity)
                    .AddProcessor(testActivityProcessor)
                    .SetSampler(new AlwaysOnSampler())
                    .Build();

        using (SuppressInstrumentationScope.Begin(true))
        {
            using var activity = new Activity(operationNameForLegacyActivity).Start();
            Assert.False(activity.IsAllDataRequested);
        }

        Assert.False(startCalled);
        Assert.False(endCalled);
    }

    [Fact]
    public void ProcessorDoesNotReceiveNotRecordDecisionSpan()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
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
                    .AddSource(activitySourceName)
                    .AddProcessor(testActivityProcessor)
                    .SetSampler(testSampler)
                    .Build();

        testSampler.SamplingAction = (samplingParameters) =>
        {
            return new SamplingResult(SamplingDecision.Drop);
        };

        using ActivitySource source = new ActivitySource(activitySourceName);
        using var activity = source.StartActivity("somename");

        Assert.NotNull(activity);
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
                Assert.True(a.IsAllDataRequested); // If Processor.OnStart is called, activity's IsAllDataRequested is set to true
                startCalled = true;
            };

        testActivityProcessor.EndAction =
            (a) =>
            {
                Assert.False(Sdk.SuppressInstrumentation);
                Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                endCalled = true;
            };

        using var emptyActivitySource = new ActivitySource(string.Empty);
        Assert.False(emptyActivitySource.HasListeners()); // No ActivityListener for empty ActivitySource added yet

        // No AddLegacyOperationName chained to TracerProviderBuilder
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddProcessor(testActivityProcessor)
                    .Build();

        Assert.False(emptyActivitySource.HasListeners()); // No listener for empty ActivitySource even after build

        using var activity = new Activity("Test");
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
                Assert.True(a.IsAllDataRequested); // If Processor.OnStart is called, activity's IsAllDataRequested is set to true
                startCalled = true;
            };

        testActivityProcessor.EndAction =
            (a) =>
            {
                Assert.False(Sdk.SuppressInstrumentation);
                Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                endCalled = true;
            };

        using var emptyActivitySource = new ActivitySource(string.Empty);
        Assert.False(emptyActivitySource.HasListeners()); // No ActivityListener for empty ActivitySource added yet

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();

        // AddLegacyOperationName chained to TracerProviderBuilder
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(sampler)
                    .AddProcessor(testActivityProcessor)
                    .AddLegacySource(operationNameForLegacyActivity)
                    .Build();

        Assert.True(emptyActivitySource.HasListeners()); // Listener for empty ActivitySource added after TracerProvider build

        using var activity = new Activity(operationNameForLegacyActivity);
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
                Assert.True(a.IsAllDataRequested); // If Processor.OnStart is called, activity's IsAllDataRequested is set to true
                startCalled = true;
            };

        testActivityProcessor.EndAction =
            (a) =>
            {
                Assert.False(Sdk.SuppressInstrumentation);
                Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                endCalled = true;
            };

        using var emptyActivitySource = new ActivitySource(string.Empty);
        Assert.False(emptyActivitySource.HasListeners()); // No ActivityListener for empty ActivitySource added yet

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();

        // AddLegacyOperationName chained to TracerProviderBuilder
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(sampler)
                    .AddSource("ABCCompany.XYZProduct.*") // Adding a wild card source
                    .AddProcessor(testActivityProcessor)
                    .AddLegacySource(operationNameForLegacyActivity)
                    .Build();

        Assert.True(emptyActivitySource.HasListeners()); // Listener for empty ActivitySource added after TracerProvider build

        using var activity = new Activity(operationNameForLegacyActivity);
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
                Assert.True(a.IsAllDataRequested); // If Processor.OnStart is called, activity's IsAllDataRequested is set to true
                startCalled = true;
            };

        testActivityProcessor.EndAction =
            (a) =>
            {
                Assert.False(Sdk.SuppressInstrumentation);
                Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                endCalled = true;
            };

        using var emptyActivitySource = new ActivitySource(string.Empty);
        Assert.False(emptyActivitySource.HasListeners()); // No ActivityListener for empty ActivitySource added yet

        var activitySourceName = Utils.GetCurrentMethodName();
        var operationNameForLegacyActivity = $"legacyActivitySource-{activitySourceName}";
        using var activitySourceForLegacyActivity = new ActivitySource(activitySourceName, "1.0.0");

        // AddLegacyOperationName chained to TracerProviderBuilder
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddLegacySource(operationNameForLegacyActivity)
                    .AddProcessor(testActivityProcessor)
                    .Build();

        Assert.True(emptyActivitySource.HasListeners()); // Listener for empty ActivitySource added after TracerProvider build

        using var activity = new Activity(operationNameForLegacyActivity);
        activity.Start();
        SetActivitySourceProperty(activity, activitySourceForLegacyActivity);
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
                Assert.True(a.IsAllDataRequested); // If Processor.OnStart is called, activity's IsAllDataRequested is set to true
                startCalled = true;
            };

        testActivityProcessor.EndAction =
            (a) =>
            {
                Assert.False(Sdk.SuppressInstrumentation);
                Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                endCalled = true;
            };

        using var emptyActivitySource = new ActivitySource(string.Empty);
        Assert.False(emptyActivitySource.HasListeners()); // No ActivityListener for empty ActivitySource added yet

        var activitySourceName = Utils.GetCurrentMethodName();
        var operationNameForLegacyActivity = $"legacyActivitySource-{activitySourceName}";
        using var activitySourceForLegacyActivity = new ActivitySource(activitySourceName, "1.0.0");

        // AddLegacyOperationName chained to TracerProviderBuilder
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .AddSource(activitySourceForLegacyActivity.Name) // Add the updated ActivitySource as a Source
                    .AddLegacySource(operationNameForLegacyActivity)
                    .AddProcessor(testActivityProcessor)
                    .Build();

        Assert.True(emptyActivitySource.HasListeners()); // Listener for empty ActivitySource added after TracerProvider build

        using var activity = new Activity(operationNameForLegacyActivity);
        activity.Start();
        SetActivitySourceProperty(activity, activitySourceForLegacyActivity);
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
                Assert.True(a.IsAllDataRequested); // If Processor.OnStart is called, activity's IsAllDataRequested is set to true
                startCalled = true;
            };

        testActivityProcessor.EndAction =
            (a) =>
            {
                Assert.False(Sdk.SuppressInstrumentation);
                Assert.True(a.IsAllDataRequested); // If Processor.OnEnd is called, activity's IsAllDataRequested is set to true
                endCalled = true;
            };

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();

        // AddLegacyOperationName chained to TracerProviderBuilder
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddProcessor(testActivityProcessor)
            .AddLegacySource(operationNameForLegacyActivity)
            .Build();

        Assert.Equal(tracerProvider, testActivityProcessor.ParentProvider);

        using var activity = new Activity(operationNameForLegacyActivity);
        activity.Start();
        activity.Stop();

        Assert.True(startCalled);
        Assert.True(endCalled);

        // As Processors can be added anytime after Provider construction, the following validates
        // the following validates that updated processors are processing the legacy activities created from here on.
        using var testActivityProcessorNew = new TestActivityProcessor();

        bool startCalledNew = false;
        bool endCalledNew = false;

        testActivityProcessorNew.StartAction =
            (a) =>
            {
                Assert.False(Sdk.SuppressInstrumentation);
                Assert.True(a.IsAllDataRequested); // If Processor.OnStart is called, activity's IsAllDataRequested is set to true
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

        var sdkProvider = (TracerProviderSdk)tracerProvider;

        Assert.True(sdkProvider.Processor is CompositeProcessor<Activity>);
        Assert.Equal(tracerProvider, sdkProvider.Processor.ParentProvider);
        Assert.Equal(tracerProvider, testActivityProcessorNew.ParentProvider);

        using var activityNew = new Activity(operationNameForLegacyActivity); // Create a new Activity with the same operation name
        activityNew.Start();
        activityNew.Stop();

        Assert.True(startCalledNew);
        Assert.True(endCalledNew);
    }

    [Fact]
    public void SdkSamplesLegacyActivityWithAlwaysOnSampler()
    {
        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOnSampler())
                    .AddLegacySource(operationNameForLegacyActivity)
                    .Build();

        using var activity = new Activity(operationNameForLegacyActivity);
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
        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOffSampler())
                    .AddLegacySource(operationNameForLegacyActivity)
                    .Build();

        using var activity = new Activity(operationNameForLegacyActivity);
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
        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();
        var sampler = new TestSampler() { SamplingAction = (samplingParameters) => new SamplingResult(samplingDecision) };

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(sampler)
                    .AddLegacySource(operationNameForLegacyActivity)
                    .Build();

        using var activity = new Activity(operationNameForLegacyActivity);
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
        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();
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
        using var activity = new Activity(operationNameForLegacyActivity);
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

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();
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

        using var activity = new Activity(operationNameForLegacyActivity).SetParentId(remoteParentId);
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

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOnSampler())
                    .AddLegacySource(operationNameForLegacyActivity)
                    .Build();

        // Create an activity with remote parent id.
        // The sampling parameters are expected to be that of the
        // parent context i.e the remote parent.

        using var activity = new Activity(operationNameForLegacyActivity).SetParentId(remoteParentId);

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

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(new AlwaysOffSampler())
                    .AddLegacySource(operationNameForLegacyActivity)
                    .Build();

        // Create an activity with remote parent id.
        // The sampling parameters are expected to be that of the
        // parent context i.e the remote parent.

        using var activity = new Activity(operationNameForLegacyActivity).SetParentId(remoteParentId);

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
        using var activityLocalParent = new Activity("TestParent")
        {
            ActivityTraceFlags = traceFlags,
            TraceStateString = tracestate,
        };
        activityLocalParent.Start();

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();
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
        using var activity = new Activity(operationNameForLegacyActivity);
        activity.Start();
        activity.Stop();
    }

    [Theory]
    [InlineData(null, null, "ParentBased{AlwaysOnSampler}")]
    [InlineData("always_on", null, "AlwaysOnSampler")]
    [InlineData("always_off", null, "AlwaysOffSampler")]
    [InlineData("always_OFF", null, "AlwaysOffSampler")]
    [InlineData("traceidratio", "0.5", "TraceIdRatioBasedSampler{0.500000}")]
    [InlineData("traceidratio", "not_a_double", "TraceIdRatioBasedSampler{1.000000}")]
    [InlineData("parentbased_always_on", null, "ParentBased{AlwaysOnSampler}")]
    [InlineData("parentbased_always_off", null, "ParentBased{AlwaysOffSampler}")]
    [InlineData("parentbased_traceidratio", "0.111", "ParentBased{TraceIdRatioBasedSampler{0.111000}}")]
    [InlineData("parentbased_traceidratio", "not_a_double", "ParentBased{TraceIdRatioBasedSampler{1.000000}}")]
    [InlineData("ParentBased_TraceIdRatio", "0.000001", "ParentBased{TraceIdRatioBasedSampler{0.000001}}")]
    public void TestSamplerSetFromConfiguration(string? configValue, string? argValue, string samplerDescription)
    {
        var configBuilder = new ConfigurationBuilder();

        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [TracerProviderSdk.TracesSamplerConfigKey] = configValue,
            [TracerProviderSdk.TracesSamplerArgConfigKey] = argValue,
        });

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => s.AddSingleton<IConfiguration>(configBuilder.Build()));
        using var tracerProvider = builder.Build();
        var tracerProviderSdk = tracerProvider as TracerProviderSdk;

        Assert.NotNull(tracerProviderSdk);
        Assert.NotNull(tracerProviderSdk.Sampler);
        Assert.Equal(samplerDescription, tracerProviderSdk.Sampler.Description);
    }

    [Fact]
    public void TestSamplerConfigurationIgnoredWhenSetProgrammatically()
    {
        var configBuilder = new ConfigurationBuilder();
        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [TracerProviderSdk.TracesSamplerConfigKey] = "always_off",
        });

        var builder = Sdk.CreateTracerProviderBuilder();
        builder.ConfigureServices(s => s.AddSingleton<IConfiguration>(configBuilder.Build()));
        builder.SetSampler(new AlwaysOnSampler());

        using var tracerProvider = builder.Build();
        var tracerProviderSdk = tracerProvider as TracerProviderSdk;

        Assert.NotNull(tracerProviderSdk);
        Assert.NotNull(tracerProviderSdk.Sampler);
        Assert.Equal("AlwaysOnSampler", tracerProviderSdk.Sampler.Description);
    }

    [Fact]
    public void TracerProvideSdkCreatesAndDiposesInstrumentation()
    {
        TestInstrumentation? testInstrumentation = null;
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

        Assert.NotNull(resource);
        Assert.NotEqual(Resource.Empty, resource);

        var attributes = resource.Attributes;
        Assert.Equal(4, attributes.Count());
        ResourceTests.ValidateDefaultAttributes(attributes);
        ResourceTests.ValidateTelemetrySdkAttributes(attributes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void AddLegacyOperationName_BadArgs(string? operationName)
    {
        var builder = Sdk.CreateTracerProviderBuilder();
        Assert.Throws<ArgumentException>(() => builder.AddLegacySource(operationName!));
    }

    [Fact]
    public void AddLegacyOperationNameAddsActivityListenerForEmptyActivitySource()
    {
        using var emptyActivitySource = new ActivitySource(string.Empty);
        var builder = Sdk.CreateTracerProviderBuilder();
        builder.AddLegacySource("TestOperationName");

        Assert.False(emptyActivitySource.HasListeners());
        using var provider = builder.Build();
        Assert.True(emptyActivitySource.HasListeners());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TracerProviderSdkBuildsWithSDKResource(bool useConfigure)
    {
        using var tracerProvider = useConfigure ?
            Sdk.CreateTracerProviderBuilder().SetResourceBuilder(
                ResourceBuilder.CreateDefault().AddTelemetrySdk()).Build() :
            Sdk.CreateTracerProviderBuilder().ConfigureResource(r => r.AddTelemetrySdk()).Build();
        var resource = tracerProvider.GetResource();
        var attributes = resource.Attributes;

        Assert.NotNull(resource);
        Assert.NotEqual(Resource.Empty, resource);
        Assert.Contains(new KeyValuePair<string, object>("telemetry.sdk.name", "opentelemetry"), attributes);
        Assert.Contains(new KeyValuePair<string, object>("telemetry.sdk.language", "dotnet"), attributes);
        var versionAttribute = attributes.Where(pair => pair.Key.Equals("telemetry.sdk.version", StringComparison.Ordinal));
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
                Assert.True(a.IsAllDataRequested); // If Processor.OnStart is called, activity's IsAllDataRequested is set to true
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
        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);

        // AddLegacyOperationName chained to TracerProviderBuilder
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(sampler)
                    .AddProcessor(testActivityProcessor)
                    .AddLegacySource(legacySourceNamespaces[0])
                    .AddLegacySource(legacySourceNamespaces[1])
                    .AddSource(activitySourceName)
                    .Build();

        foreach (var ns in legacySourceNamespaces)
        {
            var startOpName = ns.Replace("*", "Start");
            using var startOperation = new Activity(startOpName);
            startOperation.Start();
            startOperation.Stop();

            Assert.Contains(startOpName, onStartProcessedActivities); // Processor.OnStart is called since we added a legacy OperationName
            Assert.Contains(startOpName, onStopProcessedActivities);  // Processor.OnEnd is called since we added a legacy OperationName

            var stopOpName = ns.Replace("*", "Stop");
            using var stopOperation = new Activity(stopOpName);
            stopOperation.Start();
            stopOperation.Stop();

            Assert.Contains(stopOpName, onStartProcessedActivities); // Processor.OnStart is called since we added a legacy OperationName
            Assert.Contains(stopOpName, onStopProcessedActivities);  // Processor.OnEnd is called since we added a legacy OperationName
        }

        // Ensure we can still process "normal" activities when in legacy wildcard mode.
        using var nonLegacyActivity = activitySource.StartActivity("TestActivity");

        Assert.NotNull(nonLegacyActivity);
        nonLegacyActivity.Start();
        nonLegacyActivity.Stop();

        Assert.Contains(nonLegacyActivity.OperationName, onStartProcessedActivities); // Processor.OnStart is called since we added a legacy OperationName
        Assert.Contains(nonLegacyActivity.OperationName, onStopProcessedActivities);  // Processor.OnEnd is called since we added a legacy OperationName
    }

    [Fact]
    public void BuilderTypeDoesNotChangeTest()
    {
        var originalBuilder = new TestTracerProviderBuilder();

        // Tests the protected version of AddInstrumentation on TracerProviderBuilderBase
        var currentBuilder = originalBuilder.AddInstrumentation();
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        var deferredBuilder = currentBuilder as IDeferredTracerProviderBuilder;
        Assert.NotNull(deferredBuilder);

        currentBuilder = deferredBuilder.Configure((sp, innerBuilder) => { });
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.ConfigureServices(s => { });
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.AddInstrumentation(() => new object());
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.AddSource("MySource");
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.AddLegacySource("MyLegacySource");
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        using var provider = currentBuilder.Build();

        Assert.NotNull(provider);
    }

    [Fact]
    public void CheckActivityLinksAddedAfterActivityCreation()
    {
        var exportedItems = new List<Activity>();
        using var source = new ActivitySource($"{Utils.GetCurrentMethodName()}.1");
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                .SetSampler(new AlwaysOnSampler())
                .AddInMemoryExporter(exportedItems)
                .AddSource(source.Name)
                .Build();

        var link1 = new ActivityLink(new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));
        var link2 = new ActivityLink(new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded));

        using (var activity = source.StartActivity("root"))
        {
            activity?.AddLink(link1);
            activity?.AddLink(link2);
        }

        Assert.Single(exportedItems);
        var exportedActivity = exportedItems[0];
        Assert.Equal(2, exportedActivity.Links.Count());

        // verify that the links retain the order as they were added.
        Assert.Equal(link1.Context, exportedActivity.Links.ElementAt(0).Context);
        Assert.Equal(link2.Context, exportedActivity.Links.ElementAt(1).Context);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    private static Action<Activity, ActivitySource> CreateActivitySourceSetter()
    {
        var setMethod = typeof(Activity).GetProperty("Source")?.SetMethod
            ?? throw new InvalidOperationException("Could not build Activity.Source setter delegate");

#if NET
        return setMethod.CreateDelegate<Action<Activity, ActivitySource>>();
#else
        return (Action<Activity, ActivitySource>)setMethod.CreateDelegate(typeof(Action<Activity, ActivitySource>));
#endif
    }

    private sealed class TestTracerProviderBuilder : TracerProviderBuilderBase
    {
        public TracerProviderBuilder AddInstrumentation()
        {
            return this.AddInstrumentation("SomeInstrumentation", "1.0.0", () => new object());
        }
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
