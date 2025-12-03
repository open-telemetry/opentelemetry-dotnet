// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// Includes work from:
// Copyright Amazon.com, Inc. or its affiliates. All Rights Reserved.
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// This sampler will return the sampling result of the provided rootSampler, unless the
/// sampling result contains the sampling decision <see cref="SamplingDecision.Drop"/>, in which case, a
/// new sampling result will be returned that is functionally equivalent to the original, except that
/// it contains the sampling decision  <see cref="SamplingDecision.RecordOnly"/>. This ensures that all
/// spans are recorded, with no change to sampling.
///
/// The intended use case of this sampler is to provide a means of sending all spans to a
/// processor without having an impact on the sampling rate. This may be desirable if a user wishes
/// to count or otherwise measure all spans produced in a service, without incurring the cost of 100%
/// sampling.
/// </summary>
[Experimental(DiagnosticDefinitions.AlwaysRecordSamplerExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
public
#else
internal
#endif
    sealed class AlwaysRecordSampler : Sampler
{
    private readonly Sampler rootSampler;

    private AlwaysRecordSampler(Sampler rootSampler)
    {
        this.rootSampler = rootSampler;
        this.Description = "AlwaysRecordSampler{" + rootSampler.Description + "}";
    }

    /// <summary>
    /// Method to create an AlwaysRecordSampler.
    /// </summary>
    /// <param name="rootSampler"><see cref="Sampler"/>rootSampler to create AlwaysRecordSampler from.</param>
    /// <returns>Created AlwaysRecordSampler.</returns>
    public static AlwaysRecordSampler Create(Sampler rootSampler)
    {
        Guard.ThrowIfNull(rootSampler);
        return new AlwaysRecordSampler(rootSampler);
    }

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        SamplingResult result = this.rootSampler.ShouldSample(samplingParameters);
        if (result.Decision == SamplingDecision.Drop)
        {
            result = WrapResultWithRecordOnlyResult(result);
        }

        return result;
    }

    private static SamplingResult WrapResultWithRecordOnlyResult(SamplingResult result)
    {
        return new SamplingResult(SamplingDecision.RecordOnly, result.Attributes, result.TraceStateString);
    }
}