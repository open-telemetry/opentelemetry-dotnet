// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class SamplingResultTests
{
    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordAndSample)]
    [InlineData(SamplingDecision.RecordOnly)]
    public void VerifyCtor_SamplingDecision(SamplingDecision decision)
    {
        var samplingResult = new SamplingResult(decision);
        Assert.Equal(decision, samplingResult.Decision);
        Assert.Empty(samplingResult.Attributes);
    }

    [Theory]
    [InlineData(false, SamplingDecision.Drop)]
    [InlineData(true, SamplingDecision.RecordAndSample)]
    public void VerifyCtor_Bool(bool isSampled, SamplingDecision expectedSamplingDecision)
    {
        var samplingResult = new SamplingResult(isSampled);
        Assert.Equal(expectedSamplingDecision, samplingResult.Decision);
        Assert.Empty(samplingResult.Attributes);
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordAndSample)]
    [InlineData(SamplingDecision.RecordOnly)]
    public void VerifyCtor_SamplingDecisionAndAttributes(SamplingDecision decision)
    {
        var attributes = new Dictionary<string, object>
        {
            { "A", 1 },
            { "B", 2 },
            { "C", 3 },
        };

        var samplingResult = new SamplingResult(decision, attributes);
        Assert.Equal(decision, samplingResult.Decision);
        Assert.True(attributes.SequenceEqual(samplingResult.Attributes));
    }

    [Theory]
    [InlineData(SamplingDecision.Drop, true)]
    [InlineData(SamplingDecision.RecordAndSample, false)]
    [InlineData(SamplingDecision.RecordOnly, false)]
    public void VerifyOperator_Equals(SamplingDecision decision, bool expected)
    {
        var samplingResult1 = new SamplingResult(SamplingDecision.Drop);

        var samplingResult2 = new SamplingResult(decision);
        Assert.Equal(expected, samplingResult1 == samplingResult2);
    }

    [Theory]
    [InlineData(SamplingDecision.Drop, false)]
    [InlineData(SamplingDecision.RecordAndSample, true)]
    [InlineData(SamplingDecision.RecordOnly, true)]
    public void VerifyOperator_NotEquals(SamplingDecision decision, bool expected)
    {
        var samplingResult1 = new SamplingResult(SamplingDecision.Drop);

        var samplingResult2 = new SamplingResult(decision);
        Assert.Equal(expected, samplingResult1 != samplingResult2);
    }

    [Fact]
    public void Verify_Equals()
    {
        var samplingResult1 = new SamplingResult(SamplingDecision.Drop);
        Assert.True(samplingResult1.Equals(samplingResult1));
        Assert.True(samplingResult1.Equals((object)samplingResult1));

        var samplingResult2 = new SamplingResult(SamplingDecision.RecordAndSample);
        Assert.False(samplingResult1.Equals(samplingResult2));
        Assert.True(samplingResult2.Equals(samplingResult2));
        Assert.False(samplingResult1.Equals((object)samplingResult2));
        Assert.True(samplingResult2.Equals((object)samplingResult2));

        var samplingResult3 = new SamplingResult(
            SamplingDecision.RecordOnly,
            new Dictionary<string, object>
            {
                { "A", 1 },
                { "B", 2 },
                { "C", 3 },
            });
        Assert.False(samplingResult1.Equals(samplingResult3));
        Assert.True(samplingResult3.Equals(samplingResult3));
        Assert.False(samplingResult1.Equals((object)samplingResult3));
        Assert.True(samplingResult3.Equals((object)samplingResult3));

        Assert.False(samplingResult1.Equals(Guid.Empty));
    }

    [Theory]
    [InlineData(SamplingDecision.Drop)]
    [InlineData(SamplingDecision.RecordAndSample)]
    [InlineData(SamplingDecision.RecordOnly)]
    public void Verify_GetHashCode(SamplingDecision decision)
    {
        var samplingResult1 = new SamplingResult(decision);
        var samplingResult2 = new SamplingResult(decision, new Dictionary<string, object>
            {
                { "A", 1 },
                { "B", 2 },
                { "C", 3 },
            });

        Assert.NotEqual(samplingResult1.GetHashCode(), samplingResult2.GetHashCode());
    }
}
