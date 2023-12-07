// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Trace;

/// <summary>
/// Controls the number of samples of traces collected and sent to the backend.
/// </summary>
public abstract class Sampler
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Sampler"/> class.
    /// </summary>
    protected Sampler()
    {
        this.Description = this.GetType().Name;
    }

    /// <summary>
    /// Gets or sets the sampler description.
    /// </summary>
    public string Description { get; protected set; }

    /// <summary>
    /// Checks whether activity needs to be created and tracked.
    /// </summary>
    /// <param name="samplingParameters">
    /// The <see cref="SamplingParameters"/> used by the <see cref="Sampler"/>
    /// to decide if the <see cref="Activity"/> to be created is going to be sampled or not.
    /// </param>
    /// <returns>Sampling decision on whether activity needs to be sampled or not.</returns>
    public abstract SamplingResult ShouldSample(in SamplingParameters samplingParameters);
}
