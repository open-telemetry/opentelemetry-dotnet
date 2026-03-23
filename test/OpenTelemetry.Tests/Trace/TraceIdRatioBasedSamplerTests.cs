// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class TraceIdRatioBasedSamplerTests
{
    private const string ActivityDisplayName = "MyActivityName";

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
            defaultProbability.ShouldSample(new SamplingParameters(default, notSampledtraceId, ActivityDisplayName, ActivityKind.Server, null, null)).Decision);

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
            defaultProbability.ShouldSample(new SamplingParameters(default, sampledtraceId, ActivityDisplayName, ActivityKind.Server, null, null)).Decision);
    }

    [Fact]
    public void GetDescription()
    {
        var expectedDescription = "TraceIdRatioBasedSampler{0.500000}";
        Assert.Equal(expectedDescription, new TraceIdRatioBasedSampler(0.5).Description);
    }

    /// <summary>
    /// Verifies that a trace ID whose bytes produce long.MinValue is correctly dropped
    /// when probability is 0.0.
    /// </summary>
    [Fact]
    public void ShouldSample_WithZeroProbabilityAndLongMinValueTraceId_ShouldDrop()
    {
        var sampler = new TraceIdRatioBasedSampler(0.0);
        var traceId = CreateTraceIdProducingLongMinValue();

        var result = sampler.ShouldSample(new SamplingParameters(default, traceId, ActivityDisplayName, ActivityKind.Server, null, null));

        Assert.Equal(SamplingDecision.Drop, result.Decision);
    }

    /// <summary>
    /// Verifies that a trace ID whose bytes produce long.MinValue is correctly sampled
    /// when probability is 0.5. The bit pattern 0x8000000000000000 sits at the bottom of
    /// the value space and must not be treated as the largest value.
    /// </summary>
    [Fact]
    public void ShouldSample_WithHalfProbabilityAndLongMinValueTraceId_ShouldRecord()
    {
        var sampler = new TraceIdRatioBasedSampler(0.5);
        var traceId = CreateTraceIdProducingLongMinValue();

        var result = sampler.ShouldSample(new SamplingParameters(default, traceId, ActivityDisplayName, ActivityKind.Server, null, null));

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
    }

    /// <summary>
    /// Verifies that a trace ID whose bytes produce long.MinValue is correctly sampled
    /// when probability is 1.0.
    /// </summary>
    [Fact]
    public void ShouldSample_WithFullProbabilityAndLongMinValueTraceId_ShouldRecord()
    {
        var sampler = new TraceIdRatioBasedSampler(1.0);
        var traceId = CreateTraceIdProducingLongMinValue();

        var result = sampler.ShouldSample(new SamplingParameters(default, traceId, ActivityDisplayName, ActivityKind.Server, null, null));

        Assert.Equal(SamplingDecision.RecordAndSample, result.Decision);
    }

    /// <summary>
    /// Generates a trace ID whose first 8 bytes equal 0x8000000000000000, which
    /// GetLowerLong converts to long.MinValue - the value that triggers the overflow bug.
    /// </summary>
    private static ActivityTraceId CreateTraceIdProducingLongMinValue()
    {
        return ActivityTraceId.CreateFromBytes(
        [
            0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
        ]);
    }
}
