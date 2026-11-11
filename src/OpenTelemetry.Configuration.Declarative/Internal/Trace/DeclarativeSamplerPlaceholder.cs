// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Trace;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Stands in for the document's sampler until the tracer provider selects its sampler.
/// </summary>
internal sealed class DeclarativeSamplerPlaceholder : Sampler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarativeSamplerPlaceholder"/> class.
    /// </summary>
    public DeclarativeSamplerPlaceholder()
    {
        this.Description = nameof(DeclarativeSamplerPlaceholder);
    }

    /// <inheritdoc/>
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters) =>
        throw new InvalidOperationException("The sampler of the declarative configuration document was not created.");
}
