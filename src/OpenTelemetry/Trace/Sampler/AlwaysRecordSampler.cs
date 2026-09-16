// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Includes work from:
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Sampler implementation which wraps another <see cref="Sampler"/> and replaces any
/// <see cref="SamplingDecision.Drop"/> decision made by that sampler with
/// <see cref="SamplingDecision.RecordOnly"/>, leaving every other decision unchanged.
/// This ensures that all spans are recorded, without changing the sampling rate.
/// </summary>
/// <remarks>
/// The intended use case of this sampler is to provide a means of sending all spans
/// to a processor without incurring the cost of exporting 100% of spans. All spans
/// will be created and processed locally.
/// </remarks>
public sealed class AlwaysRecordSampler : Sampler
{
    private readonly Sampler rootSampler;

    /// <summary>
    /// Initializes a new instance of the <see cref="AlwaysRecordSampler"/> class.
    /// </summary>
    /// <param name="rootSampler">The <see cref="Sampler"/> whose sampling decisions should always be recorded.</param>
    public AlwaysRecordSampler(Sampler rootSampler)
    {
        Guard.ThrowIfNull(rootSampler);

        this.rootSampler = rootSampler;
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        this.Description = "AlwaysRecordSampler{" + rootSampler.Description + "}";
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
    }

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var result = this.rootSampler.ShouldSample(samplingParameters);

        return result.Decision switch
        {
            SamplingDecision.Drop => new(SamplingDecision.RecordOnly, result.AttributesOrNull, result.TraceStateString),
            SamplingDecision.RecordOnly or SamplingDecision.RecordAndSample => result,
            _ => result, // Undefined behaviour - invalid value
        };
    }
}
