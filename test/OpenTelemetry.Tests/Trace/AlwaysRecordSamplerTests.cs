// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Includes work from:
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Trace.Tests;

public class AlwaysRecordSamplerTests
{
    [Fact]
    public void ConstructorThrowsOnNullRootSampler()
        => Assert.Throws<ArgumentNullException>(() => new AlwaysRecordSampler(null!));

    [Fact]
    public void DescriptionIncludesRootSamplerDescription()
    {
        var sampler = new AlwaysRecordSampler(new TestSampler());

        Assert.Equal("AlwaysRecordSampler{TestSampler}", sampler.Description);
    }

    [Theory]
    [InlineData(SamplingDecision.Drop, SamplingDecision.RecordOnly)]
    [InlineData(SamplingDecision.RecordAndSample, SamplingDecision.RecordAndSample)]
    [InlineData(SamplingDecision.RecordOnly, SamplingDecision.RecordOnly)]
    public void ShouldSampleReplacesDropWithRecordOnly(SamplingDecision rootDecision, SamplingDecision expectedDecision)
    {
        var attributes = new List<KeyValuePair<string, object>> { new("key", "value") };
        var rootResult = new SamplingResult(rootDecision, attributes, "traceState");
        var testSampler = new TestSampler { SamplingAction = _ => rootResult };
        var sampler = new AlwaysRecordSampler(testSampler);

        var samplingParameters = new SamplingParameters(
            default, default, "name", ActivityKind.Client, [], []);

        var actualResult = sampler.ShouldSample(samplingParameters);

        Assert.Equal(expectedDecision, actualResult.Decision);
        Assert.Equal(rootResult.Attributes, actualResult.Attributes);
        Assert.Equal(rootResult.TraceStateString, actualResult.TraceStateString);
    }
}
