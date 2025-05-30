// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// Sampling result.
/// </summary>
public readonly struct SamplingResult : IEquatable<SamplingResult>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingResult"/> struct.
    /// </summary>
    /// <param name="decision"> indicates whether an activity object is recorded and sampled.</param>
    public SamplingResult(SamplingDecision decision)
        : this(decision, attributes: null, traceStateString: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingResult"/> struct.
    /// </summary>
    /// <param name="isSampled"> True if sampled, false otherwise.</param>
    public SamplingResult(bool isSampled)
        : this(decision: isSampled ? SamplingDecision.RecordAndSample : SamplingDecision.Drop, attributes: null, traceStateString: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingResult"/> struct.
    /// </summary>
    /// <param name="decision">indicates whether an activity object is recorded and sampled.</param>
    /// <param name="attributes">Attributes associated with the sampling decision. Attributes list passed to
    /// this method must be immutable. Mutations of the collection and/or attribute values may lead to unexpected behavior.</param>
    public SamplingResult(SamplingDecision decision, IEnumerable<KeyValuePair<string, object>>? attributes)
        : this(decision, attributes, traceStateString: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingResult"/> struct.
    /// </summary>
    /// <param name="decision">indicates whether an activity object is recorded and sampled.</param>
    /// <param name="traceStateString">traceStateString associated with the created Activity.</param>
    public SamplingResult(SamplingDecision decision, string? traceStateString)
        : this(decision, attributes: null, traceStateString)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplingResult"/> struct.
    /// </summary>
    /// <param name="decision">indicates whether an activity object is recorded and sampled.</param>
    /// <param name="attributes">attributes associated with the sampling decision. Attributes list passed to
    /// this method must be immutable. Mutations of the collection and/or attribute values may lead to unexpected behavior.</param>
    /// <param name="traceStateString">traceStateString associated with the created Activity.</param>
    public SamplingResult(SamplingDecision decision, IEnumerable<KeyValuePair<string, object>>? attributes, string? traceStateString)
    {
        this.Decision = decision;

        // Note: Decision object takes ownership of the collection.
        // Current implementation has no means to ensure the collection will not be modified by the caller.
        // If this behavior will be abused we must switch to cloning of the collection.
        this.Attributes = attributes ?? Enumerable.Empty<KeyValuePair<string, object>>();

        this.TraceStateString = traceStateString;
    }

    /// <summary>
    /// Gets a value indicating whether an activity object is recorded and sampled.
    /// </summary>
    public SamplingDecision Decision { get; }

    /// <summary>
    /// Gets a map of attributes associated with the sampling decision.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>> Attributes { get; }

    /// <summary>
    /// Gets the tracestate.
    /// </summary>
    public string? TraceStateString { get; }

    /// <summary>
    /// Compare two <see cref="SamplingResult"/> for equality.
    /// </summary>
    /// <param name="decision1">First Decision to compare.</param>
    /// <param name="decision2">Second Decision to compare.</param>
    public static bool operator ==(SamplingResult decision1, SamplingResult decision2) => decision1.Equals(decision2);

    /// <summary>
    /// Compare two <see cref="SamplingResult"/> for not equality.
    /// </summary>
    /// <param name="decision1">First Decision to compare.</param>
    /// <param name="decision2">Second Decision to compare.</param>
    public static bool operator !=(SamplingResult decision1, SamplingResult decision2) => !decision1.Equals(decision2);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is SamplingResult other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
#if NET || NETSTANDARD2_1_OR_GREATER
        HashCode hashCode = default;
        hashCode.Add(this.Decision);
        hashCode.Add(this.Attributes);
        hashCode.Add(this.TraceStateString);

        var hash = hashCode.ToHashCode();
#else
        var hash = 17;
        unchecked
        {
            hash = (31 * hash) + this.Decision.GetHashCode();
            hash = (31 * hash) + this.Attributes.GetHashCode();
            hash = (31 * hash) + (this.TraceStateString?.GetHashCode() ?? 0);
        }
#endif
        return hash;
    }

    /// <inheritdoc/>
    public bool Equals(SamplingResult other)
    {
        return this.Decision == other.Decision
            && this.Attributes.SequenceEqual(other.Attributes)
            && this.TraceStateString == other.TraceStateString;
    }
}
