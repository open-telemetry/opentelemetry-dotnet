// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class TraceIdRatioBasedSamplerTests
{
    private const string ActivityDisplayName = "MyActivityName";
    private static readonly ActivityKind ActivityKindServer = ActivityKind.Server;

    [Fact]
    public void OutOfRangeHighProbability()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TraceIdRatioBasedSampler(1.01));
    }

    [Fact]
    public void OutOfRangeLowProbability()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new TraceIdRatioBasedSampler(-0.00001));
    }

    [Fact]
    public void SampleBasedOnTraceId()
    {
        Sampler defaultProbability = new TraceIdRatioBasedSampler(0.0001);

        // This traceId will not be sampled by the TraceIdRatioBasedSampler because the first 8 bytes as long
        // is not less than probability * Long.MAX_VALUE;
        var notSampledtraceId =
            ActivityTraceId.CreateFromBytes(
            [
                0x8F,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0
            ]);
        Assert.Equal(
            SamplingDecision.Drop,
            defaultProbability.ShouldSample(new SamplingParameters(default, notSampledtraceId, ActivityDisplayName, ActivityKindServer, null, null)).Decision);

        // This traceId will be sampled by the TraceIdRatioBasedSampler because the first 8 bytes as long
        // is less than probability * Long.MAX_VALUE;
        var sampledtraceId =
            ActivityTraceId.CreateFromBytes(
            [
                0x00,
                0x00,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0xFF,
                0,
                0,
                0,
                0,
                0,
                0,
                0,
                0
            ]);
        Assert.Equal(
            SamplingDecision.RecordAndSample,
            defaultProbability.ShouldSample(new SamplingParameters(default, sampledtraceId, ActivityDisplayName, ActivityKindServer, null, null)).Decision);
    }

    [Fact]
    public void GetDescription()
    {
        var expectedDescription = "TraceIdRatioBasedSampler{0.500000}";
        Assert.Equal(expectedDescription, new TraceIdRatioBasedSampler(0.5).Description);
    }
}
