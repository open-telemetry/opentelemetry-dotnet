// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Trace;

/// <summary>
/// Sampling parameters passed to a <see cref="Sampler"/> for it to make a sampling decision.
/// </summary>
public readonly struct SamplingParameters : IEquatable<SamplingParameters>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingParameters"/> struct.
    /// </summary>
    /// <param name="parentContext">Parent activity context. Typically taken from the wire.</param>
    /// <param name="traceId">Trace ID of a activity to be created.</param>
    /// <param name="name">The name (DisplayName) of the activity to be created. Note, that the name of the activity is settable.
    /// So this name can be changed later and Sampler implementation should assume that.
    /// Typical example of a name change is when <see cref="Activity"/> representing incoming http request
    /// has a name of url path and then being updated with route name when routing complete.
    /// </param>
    /// <param name="kind">The kind of the Activity to be created.</param>
    /// <param name="tags">Initial set of Tags for the Activity being constructed.</param>
    /// <param name="links">Links associated with the activity.</param>
    public SamplingParameters(
        ActivityContext parentContext,
        ActivityTraceId traceId,
        string name,
        ActivityKind kind,
        IEnumerable<KeyValuePair<string, object?>>? tags = null,
        IEnumerable<ActivityLink>? links = null)
    {
        this.ParentContext = parentContext;
        this.TraceId = traceId;
        this.Kind = kind;
        this.Tags = tags;
        this.Links = links;

        // Note: myActivitySource.StartActivity(name: null) is currently
        // allowed even though OTel spec says span name is required. See:
        // https://github.com/open-telemetry/opentelemetry-dotnet/issues/3802
        this.Name = name ?? string.Empty;
    }

    /// <summary>
    /// Gets the parent activity context.
    /// </summary>
    public ActivityContext ParentContext { get; }

    /// <summary>
    /// Gets the trace ID of parent activity or a new generated one for root span/activity.
    /// </summary>
    public ActivityTraceId TraceId { get; }

    /// <summary>
    /// Gets the name to be given to the span/activity.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the kind of span/activity to be created.
    /// </summary>
    /// <remarks>
    /// For Activities created outside of ActivitySource,
    /// the Kind will be the default (Internal).
    /// </remarks>
    public ActivityKind Kind { get; }

    /// <summary>
    /// Gets the tags to be associated to the span/activity to be created.
    /// These are the tags provided at the time of Activity creation.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object?>>? Tags { get; }

    /// <summary>
    /// Gets the links to be added to the activity to be created.
    /// </summary>
    public IEnumerable<ActivityLink>? Links { get; }

    /// <summary>
    /// Compare two <see cref="SamplingParameters"/> for equality.
    /// </summary>
    /// <param name="parameters1">First parameters to compare.</param>
    /// <param name="parameters2">Second parameters to compare.</param>
    public static bool operator ==(SamplingParameters parameters1, SamplingParameters parameters2) => parameters1.Equals(parameters2);

    /// <summary>
    /// Compare two <see cref="SamplingParameters"/> for not equality.
    /// </summary>
    /// <param name="parameters1">First parameters to compare.</param>
    /// <param name="parameters2">Second parameters to compare.</param>
    public static bool operator !=(SamplingParameters parameters1, SamplingParameters parameters2) => !parameters1.Equals(parameters2);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is SamplingParameters other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        // Note: Tags and Links are deliberately not included in the hash code
        // because equality for them is based on their content. Enumerating
        // them here would add cost on hot paths and hashing the enumerable
        // instances would not be consistent with Equals.
#if NET || NETSTANDARD2_1_OR_GREATER
        return HashCode.Combine(this.ParentContext, this.TraceId, this.Name, this.Kind);
#else
        var hash = 17;
        unchecked
        {
            hash = (31 * hash) + this.ParentContext.GetHashCode();
            hash = (31 * hash) + this.TraceId.GetHashCode();
            hash = (31 * hash) + (this.Name?.GetHashCode() ?? 0);
            hash = (31 * hash) + this.Kind.GetHashCode();
        }

        return hash;
#endif
    }

    /// <inheritdoc/>
    public bool Equals(SamplingParameters other) =>
        this.ParentContext == other.ParentContext &&
        this.TraceId == other.TraceId &&
        this.Name == other.Name &&
        this.Kind == other.Kind &&
        (this.Tags ?? []).SequenceEqual(other.Tags ?? []) &&
        (this.Links ?? []).SequenceEqual(other.Links ?? []);
}
