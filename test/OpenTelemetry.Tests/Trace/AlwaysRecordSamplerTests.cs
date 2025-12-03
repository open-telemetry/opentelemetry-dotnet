// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Includes work from:
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

/// <summary>
/// AlwaysRecordSamplerTest test class.
/// </summary>
public class AlwaysRecordSamplerTests
{
    /// <summary>
    /// Tests Description is set properly with AlwaysRecordSampler keyword.
    /// </summary>
    [Fact]
    public void TestGetDescription()
    {
        var testSampler = new TestSampler();
        var sampler = AlwaysRecordSampler.Create(testSampler);
        Assert.Equal("AlwaysRecordSampler{TestSampler}", sampler.Description);
    }

    /// <summary>
    /// Test RECORD_AND_SAMPLE sampling decision.
    /// </summary>
    [Fact]
    public void TestRecordAndSampleSamplingDecision()
    {
        ValidateShouldSample(SamplingDecision.RecordAndSample, SamplingDecision.RecordAndSample);
    }

    /// <summary>
    /// Test RECORD_ONLY sampling decision.
    /// </summary>
    [Fact]
    public void TestRecordOnlySamplingDecision()
    {
        ValidateShouldSample(SamplingDecision.RecordOnly, SamplingDecision.RecordOnly);
    }

    /// <summary>
    /// Test DROP sampling decision.
    /// </summary>
    [Fact]
    public void TestDropSamplingDecision()
    {
        ValidateShouldSample(SamplingDecision.Drop, SamplingDecision.RecordOnly);
    }

    private static SamplingResult BuildRootSamplingResult(SamplingDecision samplingDecision)
    {
        ActivityTagsCollection? attributes = new ActivityTagsCollection
        {
            { "key", samplingDecision.GetType().Name },
        };
        string traceState = samplingDecision.GetType().Name;
#pragma warning disable CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
        return new SamplingResult(samplingDecision, attributes, traceState);
#pragma warning restore CS8620 // Argument cannot be used for parameter due to differences in the nullability of reference types.
    }

    private static void ValidateShouldSample(
        SamplingDecision rootDecision, SamplingDecision expectedDecision)
    {
        SamplingResult rootResult = BuildRootSamplingResult(rootDecision);
        var testSampler = new TestSampler { SamplingAction = _ => rootResult };
        var sampler = AlwaysRecordSampler.Create(testSampler);

        SamplingParameters samplingParameters = new SamplingParameters(
            default, default, "name", ActivityKind.Client, new ActivityTagsCollection(), new List<ActivityLink>());

        SamplingResult actualResult = sampler.ShouldSample(samplingParameters);

        if (rootDecision.Equals(expectedDecision))
        {
            Assert.True(actualResult.Equals(rootResult));
            Assert.True(actualResult.Decision.Equals(rootDecision));
        }
        else
        {
            Assert.False(actualResult.Equals(rootResult));
            Assert.True(actualResult.Decision.Equals(expectedDecision));
        }

        Assert.Equal(rootResult.Attributes, actualResult.Attributes);
        Assert.Equal(rootDecision.GetType().Name, actualResult.TraceStateString);
    }
}
