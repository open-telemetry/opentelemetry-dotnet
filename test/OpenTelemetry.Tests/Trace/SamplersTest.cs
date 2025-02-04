// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class SamplersTest
{
    private static readonly ActivityKind ActivityKindServer = ActivityKind.Server;
    private readonly ActivityTraceId traceId;
    private readonly ActivitySpanId parentSpanId;

    public SamplersTest()
    {
        this.traceId = ActivityTraceId.CreateRandom();
        this.parentSpanId = ActivitySpanId.CreateRandom();
    }

    [Theory]
    [InlineData(ActivityTraceFlags.Recorded)]
    [InlineData(ActivityTraceFlags.None)]
    public void AlwaysOnSampler_AlwaysReturnTrue(ActivityTraceFlags flags)
    {
        var parentContext = new ActivityContext(this.traceId, this.parentSpanId, flags);
        var link = new ActivityLink(parentContext);

        Assert.Equal(
            SamplingDecision.RecordAndSample,
            new AlwaysOnSampler().ShouldSample(new SamplingParameters(parentContext, this.traceId, "Another name", ActivityKindServer, null, new List<ActivityLink> { link })).Decision);
    }

    [Fact]
    public void AlwaysOnSampler_GetDescription()
    {
        Assert.Equal("AlwaysOnSampler", new AlwaysOnSampler().Description);
    }

    [Theory]
    [InlineData(ActivityTraceFlags.Recorded)]
    [InlineData(ActivityTraceFlags.None)]
    public void AlwaysOffSampler_AlwaysReturnFalse(ActivityTraceFlags flags)
    {
        var parentContext = new ActivityContext(this.traceId, this.parentSpanId, flags);
        var link = new ActivityLink(parentContext);

        Assert.Equal(
            SamplingDecision.Drop,
            new AlwaysOffSampler().ShouldSample(new SamplingParameters(parentContext, this.traceId, "Another name", ActivityKindServer, null, new List<ActivityLink> { link })).Decision);
    }

    [Fact]
    public void AlwaysOffSampler_GetDescription()
    {
        Assert.Equal("AlwaysOffSampler", new AlwaysOffSampler().Description);
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void TracerProviderSdkSamplerAttributesAreAppliedToLegacyActivity(SamplingDecision samplingDecision)
    {
        var testSampler = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                var attributes = new Dictionary<string, object>
                {
                    { "tagkeybysampler", "tagvalueaddedbysampler" },
                };
                return new SamplingResult(samplingDecision, attributes);
            },
        };

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
                    .SetSampler(testSampler)
                    .AddLegacySource(operationNameForLegacyActivity)
                    .Build();

        using Activity activity = new Activity(operationNameForLegacyActivity);
        activity.Start();
        Assert.NotNull(activity);
        if (samplingDecision != SamplingDecision.Drop)
        {
            Assert.Contains(new KeyValuePair<string, object?>("tagkeybysampler", "tagvalueaddedbysampler"), activity.TagObjects);
        }

        activity.Stop();
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void SamplersCanModifyTraceStateOnLegacyActivity(SamplingDecision samplingDecision)
    {
        RunLegacyActivitySamplerTest(samplingDecision, samplerTraceState: "a=1,b=2,c=3,d=4");
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void SamplersDoesNotImpactTraceStateWhenUsingNullLegacyActivity(SamplingDecision samplingDecision)
    {
        RunLegacyActivitySamplerTest(samplingDecision, samplerTraceState: null);
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void SamplersCanModifyTraceState(SamplingDecision sampling)
    {
        var parentTraceState = "a=1,b=2";
        var newTraceState = "a=1,b=2,c=3,d=4";
        var testSampler = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                if (samplingParams.Name == "root")
                {
                    Assert.Equal(parentTraceState, samplingParams.ParentContext.TraceState);
                }
                else
                {
                    Assert.Equal(newTraceState, samplingParams.ParentContext.TraceState);
                }

                return new SamplingResult(sampling, newTraceState);
            },
        };

        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(testSampler)
            .Build();

        // Note: Remote parent is set as recorded
        var parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, parentTraceState, isRemote: true);

        using var root = activitySource.StartActivity("root", ActivityKind.Server, parentContext);

        // Note: We always create a root even for Drop. When dropping the
        // created root is for propagation only
        Assert.NotNull(root);
        Assert.Equal(newTraceState, root.TraceStateString);

        if (sampling == SamplingDecision.RecordAndSample)
        {
            Assert.True(root.Recorded);
            Assert.True(root.IsAllDataRequested);
        }
        else if (sampling == SamplingDecision.RecordOnly)
        {
            // TODO: Update this when repo consumes DS v10.
            // Note: Seems to be a bug in DiagnosticSource. Root in this case
            // inherits context from the remote parent and Recorded doesn't get
            // cleared. This should be fixed in .NET 10:
            // https://github.com/dotnet/runtime/pull/111289
            // Assert.False(root.Recorded);

            Assert.True(root.IsAllDataRequested);
        }
        else
        {
            // TODO: Update this when repo consumes DS v10.
            // Note: Seems to be a bug in DiagnosticSource. Root in this case
            // inherits context from the remote parent and Recorded doesn't get
            // cleared. This should be fixed in .NET 10:
            // https://github.com/dotnet/runtime/pull/111289
            // Assert.False(root.Recorded);

            Assert.False(root.IsAllDataRequested);
        }

        using var child = activitySource.StartActivity("child", ActivityKind.Server);

        if (sampling != SamplingDecision.Drop)
        {
            Assert.NotNull(child);
            Assert.Equal(newTraceState, child.TraceStateString);
        }
        else
        {
            Assert.Null(child);
        }
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample)]
    public void SamplersDoesNotImpactTraceStateWhenUsingNull(SamplingDecision sampling)
    {
        var parentTraceState = "a=1,b=2";
        var testSampler = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                Assert.Equal(parentTraceState, samplingParams.ParentContext.TraceState);

                // Not explicitly setting tracestate, leaving it null.
                // backward compat test that existing
                // samplers will not inadvertently
                // reset Tracestate
                return new SamplingResult(sampling);
            },
        };

        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(testSampler)
            .Build();

        var parentContext = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded, parentTraceState, true);

        using var activity = activitySource.StartActivity("root", ActivityKind.Server, parentContext);
        if (sampling != SamplingDecision.Drop)
        {
            Assert.NotNull(activity);
            Assert.Equal(parentTraceState, activity.TraceStateString);
        }
    }

    [Fact]
    public void SamplerExceptionBubblesUpTest()
    {
        // Note: This test verifies there is NO try/catch around sampling
        // and it will throw. For the discussion behind this see:
        // https://github.com/open-telemetry/opentelemetry-dotnet/pull/4072

        var activitySourceName = Utils.GetCurrentMethodName();
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(new ThrowingSampler())
            .Build();

        Assert.Throws<InvalidOperationException>(() => activitySource.StartActivity("ThrowingSampler"));
    }

    private static void RunLegacyActivitySamplerTest(SamplingDecision samplingDecision, string? samplerTraceState)
    {
        var existingTraceState = "a=1,b=2";

        var operationNameForLegacyActivity = Utils.GetCurrentMethodName();

        var testSampler = new TestSampler
        {
            SamplingAction = (samplingParams) =>
            {
                Assert.Equal(samplingParams.Name, operationNameForLegacyActivity);
                Assert.Equal(existingTraceState, samplingParams.ParentContext.TraceState);
                return new SamplingResult(samplingDecision, samplerTraceState);
            },
        };

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .SetSampler(testSampler)
            .AddLegacySource(operationNameForLegacyActivity)
            .Build();

        using var parentActivity = new Activity("Foo");
        parentActivity.TraceStateString = existingTraceState;
        parentActivity.Start();

        using var childActivity = new Activity(operationNameForLegacyActivity);
        childActivity.Start();

        if (samplerTraceState != null)
        {
            Assert.Equal(samplerTraceState, childActivity.TraceStateString);
        }
        else
        {
            Assert.Equal(existingTraceState, childActivity.TraceStateString);
        }

        if (samplingDecision == SamplingDecision.RecordAndSample)
        {
            Assert.True(childActivity.Recorded);
            Assert.True(childActivity.IsAllDataRequested);
        }
        else if (samplingDecision == SamplingDecision.RecordOnly)
        {
            Assert.False(childActivity.Recorded);
            Assert.True(childActivity.IsAllDataRequested);
        }
        else
        {
            Assert.False(childActivity.Recorded);
            Assert.False(childActivity.IsAllDataRequested);
        }
    }

    private sealed class ThrowingSampler : Sampler
    {
        public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
        {
            throw new InvalidOperationException("ThrowingSampler");
        }
    }
}
