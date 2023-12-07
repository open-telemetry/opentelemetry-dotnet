// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class ParentBasedSamplerTests
{
    private readonly ParentBasedSampler parentBasedOnSampler = new(new AlwaysOnSampler());
    private readonly ParentBasedSampler parentBasedOffSampler = new(new AlwaysOffSampler());

    [Fact]
    public void SampledParent()
    {
        // No parent, use delegate sampler.
        Assert.Equal(
            new SamplingResult(SamplingDecision.RecordAndSample),
            this.parentBasedOnSampler.ShouldSample(default));

        // No parent, use delegate sampler.
        Assert.Equal(
            new SamplingResult(SamplingDecision.Drop),
            this.parentBasedOffSampler.ShouldSample(default));

        // Not sampled parent, don't sample.
        Assert.Equal(
            new SamplingResult(SamplingDecision.Drop),
            this.parentBasedOnSampler.ShouldSample(
                new SamplingParameters(
                    parentContext: new ActivityContext(
                        ActivityTraceId.CreateRandom(),
                        ActivitySpanId.CreateRandom(),
                        ActivityTraceFlags.None),
                    traceId: default,
                    name: "Span",
                    kind: ActivityKind.Client)));

        // Sampled parent, sample.
        Assert.Equal(
            new SamplingResult(SamplingDecision.RecordAndSample),
            this.parentBasedOffSampler.ShouldSample(
                new SamplingParameters(
                    parentContext: new ActivityContext(
                        ActivityTraceId.CreateRandom(),
                        ActivitySpanId.CreateRandom(),
                        ActivityTraceFlags.Recorded),
                    traceId: default,
                    name: "Span",
                    kind: ActivityKind.Client)));
    }

    /// <summary>
    /// Checks fix for https://github.com/open-telemetry/opentelemetry-dotnet/issues/1846.
    /// </summary>
    [Fact]
    public void DoNotExamineLinks()
    {
        var sampledLink = new ActivityLink[]
        {
            new ActivityLink(
                new ActivityContext(
                    ActivityTraceId.CreateRandom(),
                    ActivitySpanId.CreateRandom(),
                    ActivityTraceFlags.Recorded)),
        };

        var notSampledParent = new ActivityContext(
            ActivityTraceId.CreateRandom(),
            ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.None);

        // Parent is not sampled - default behavior should be to DROP,
        // even if a sampled linked activity exists.
        Assert.Equal(
            new SamplingResult(SamplingDecision.Drop),
            this.parentBasedOffSampler.ShouldSample(
                new SamplingParameters(
                    parentContext: notSampledParent,
                    traceId: default,
                    name: "Span",
                    kind: ActivityKind.Client,
                    links: sampledLink)));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void CustomSamplers(bool parentIsRemote, bool parentIsSampled)
    {
        var remoteParentSampled = new TestSampler();
        var remoteParentNotSampled = new TestSampler();
        var localParentSampled = new TestSampler();
        var localParentNotSampled = new TestSampler();

        var samplerUnderTest = new ParentBasedSampler(
            new AlwaysOnSampler(), // root
            remoteParentSampled,
            remoteParentNotSampled,
            localParentSampled,
            localParentNotSampled);

        var samplingParams = MakeTestParameters(parentIsRemote, parentIsSampled);
        var expectedResult = new SamplingResult(SamplingDecision.RecordAndSample);
        var actualResult = samplerUnderTest.ShouldSample(samplingParams);

        Assert.Equal(parentIsRemote && parentIsSampled, remoteParentSampled.LatestSamplingParameters.Equals(samplingParams));
        Assert.Equal(parentIsRemote && !parentIsSampled, remoteParentNotSampled.LatestSamplingParameters.Equals(samplingParams));
        Assert.Equal(!parentIsRemote && parentIsSampled, localParentSampled.LatestSamplingParameters.Equals(samplingParams));
        Assert.Equal(!parentIsRemote && !parentIsSampled, localParentNotSampled.LatestSamplingParameters.Equals(samplingParams));

        Assert.Equal(expectedResult, actualResult);
    }

    [Fact]
    public void DisallowNullRootSampler()
    {
        Assert.Throws<ArgumentNullException>(() => new ParentBasedSampler(null));
    }

    private static SamplingParameters MakeTestParameters(bool parentIsRemote, bool parentIsSampled)
    {
        return new SamplingParameters(
            parentContext: new ActivityContext(
                ActivityTraceId.CreateRandom(),
                ActivitySpanId.CreateRandom(),
                parentIsSampled ? ActivityTraceFlags.Recorded : ActivityTraceFlags.None,
                null,
                parentIsRemote),
            traceId: default,
            name: "Span",
            kind: ActivityKind.Client);
    }
}