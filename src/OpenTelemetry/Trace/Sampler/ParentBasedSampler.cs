// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Sampler implementation which by default will take a sample if parent Activity is sampled.
/// Otherwise, samples root traces according to the specified root sampler.
/// </summary>
/// <remarks>
/// The default behavior can be customized by providing additional samplers to be invoked for different
/// combinations of local/remote parent and its sampling decision.
/// See <see cref="ParentBasedSampler(Sampler, Sampler, Sampler, Sampler, Sampler)"/>.
/// </remarks>
public sealed class ParentBasedSampler : Sampler
{
    private readonly Sampler rootSampler;

    private readonly Sampler remoteParentSampled;
    private readonly Sampler remoteParentNotSampled;
    private readonly Sampler localParentSampled;
    private readonly Sampler localParentNotSampled;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentBasedSampler"/> class.
    /// </summary>
    /// <param name="rootSampler">The <see cref="Sampler"/> to be called for root span/activity.</param>
    public ParentBasedSampler(Sampler rootSampler)
    {
        Guard.ThrowIfNull(rootSampler);

        this.rootSampler = rootSampler;
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        this.Description = $"ParentBased{{{rootSampler.Description}}}";
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1

        this.remoteParentSampled = new AlwaysOnSampler();
        this.remoteParentNotSampled = new AlwaysOffSampler();
        this.localParentSampled = new AlwaysOnSampler();
        this.localParentNotSampled = new AlwaysOffSampler();
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParentBasedSampler"/> class with ability to delegate
    /// sampling decision to one of the inner samplers provided.
    /// </summary>
    /// <param name="rootSampler">The <see cref="Sampler"/> to be called for root span/activity.</param>
    /// <param name="remoteParentSampled">
    /// A <see cref="Sampler"/> to delegate sampling decision to in case of
    /// remote parent (<see cref="ActivityContext.IsRemote"/> == true) with <see cref="ActivityTraceFlags.Recorded"/> flag == true.
    /// Default: <see cref="AlwaysOnSampler"/>.
    /// </param>
    /// <param name="remoteParentNotSampled">
    /// A <see cref="Sampler"/> to delegate sampling decision to in case of
    /// remote parent (<see cref="ActivityContext.IsRemote"/> == true) with <see cref="ActivityTraceFlags.Recorded"/> flag == false.
    /// Default: <see cref="AlwaysOffSampler"/>.
    /// </param>
    /// <param name="localParentSampled">
    /// A <see cref="Sampler"/> to delegate sampling decision to in case of
    /// local parent (<see cref="ActivityContext.IsRemote"/> == false) with <see cref="ActivityTraceFlags.Recorded"/> flag == true.
    /// Default: <see cref="AlwaysOnSampler"/>.
    /// </param>
    /// <param name="localParentNotSampled">
    /// A <see cref="Sampler"/> to delegate sampling decision to in case of
    /// local parent (<see cref="ActivityContext.IsRemote"/> == false) with <see cref="ActivityTraceFlags.Recorded"/> flag == false.
    /// Default: <see cref="AlwaysOffSampler"/>.
    /// </param>
    public ParentBasedSampler(
        Sampler rootSampler,
        Sampler? remoteParentSampled = null,
        Sampler? remoteParentNotSampled = null,
        Sampler? localParentSampled = null,
        Sampler? localParentNotSampled = null)
        : this(rootSampler)
    {
        this.remoteParentSampled = remoteParentSampled ?? new AlwaysOnSampler();
        this.remoteParentNotSampled = remoteParentNotSampled ?? new AlwaysOffSampler();
        this.localParentSampled = localParentSampled ?? new AlwaysOnSampler();
        this.localParentNotSampled = localParentNotSampled ?? new AlwaysOffSampler();
    }

    /// <inheritdoc />
    public override SamplingResult ShouldSample(in SamplingParameters samplingParameters)
    {
        var parentContext = samplingParameters.ParentContext;
        if (parentContext.TraceId == default)
        {
            // If no parent, use the rootSampler to determine sampling.
            return this.rootSampler.ShouldSample(samplingParameters);
        }

        // Is parent sampled?
        if ((parentContext.TraceFlags & ActivityTraceFlags.Recorded) != 0)
        {
            if (parentContext.IsRemote)
            {
                return this.remoteParentSampled.ShouldSample(samplingParameters);
            }
            else
            {
                return this.localParentSampled.ShouldSample(samplingParameters);
            }
        }

        // If parent is not sampled => delegate to the "not sampled" inner samplers.
        if (parentContext.IsRemote)
        {
            return this.remoteParentNotSampled.ShouldSample(samplingParameters);
        }
        else
        {
            return this.localParentNotSampled.ShouldSample(samplingParameters);
        }
    }
}
