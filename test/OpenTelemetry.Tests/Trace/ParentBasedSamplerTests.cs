// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Moq;
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
        var mockRepository = new MockRepository(MockBehavior.Strict);
        var remoteParentSampled = mockRepository.Create<Sampler>();
        var remoteParentNotSampled = mockRepository.Create<Sampler>();
        var localParentSampled = mockRepository.Create<Sampler>();
        var localParentNotSampled = mockRepository.Create<Sampler>();

        var samplerUnderTest = new ParentBasedSampler(
            new AlwaysOnSampler(), // root
            remoteParentSampled.Object,
            remoteParentNotSampled.Object,
            localParentSampled.Object,
            localParentNotSampled.Object);

        var samplingParams = MakeTestParameters(parentIsRemote, parentIsSampled);

        Mock<Sampler> invokedSampler;
        if (parentIsRemote && parentIsSampled)
        {
            invokedSampler = remoteParentSampled;
        }
        else if (parentIsRemote && !parentIsSampled)
        {
            invokedSampler = remoteParentNotSampled;
        }
        else if (!parentIsRemote && parentIsSampled)
        {
            invokedSampler = localParentSampled;
        }
        else
        {
            invokedSampler = localParentNotSampled;
        }

        var expectedResult = new SamplingResult(SamplingDecision.RecordAndSample);
        invokedSampler.Setup(sampler => sampler.ShouldSample(samplingParams)).Returns(expectedResult);

        var actualResult = samplerUnderTest.ShouldSample(samplingParams);

        mockRepository.VerifyAll();
        Assert.Equal(expectedResult, actualResult);
        mockRepository.VerifyNoOtherCalls();
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
