// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// Baggage entry consisting of a value with optional metadata.
/// </summary>
public readonly struct BaggageEntry : IEquatable<BaggageEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaggageEntry"/> struct.
    /// </summary>
    /// <param name="value">Entry value.</param>
    /// <param name="metadata">Entry metadata.</param>
    public BaggageEntry(string value, BaggageEntryMetadata? metadata = null)
    {
        this.Value = value;
        this.Metadata = metadata;
    }

    /// <summary>
    /// Gets the value of the BaggageEntry.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the metadata of the BaggageEntry.
    /// </summary>
    public BaggageEntryMetadata? Metadata { get; }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static bool operator ==(BaggageEntry left, BaggageEntry right) => left.Equals(right);

    public static bool operator !=(BaggageEntry left, BaggageEntry right) => !left.Equals(right);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <inheritdoc />
    public bool Equals(BaggageEntry other) =>
        string.Equals(this.Value, other.Value) &&
        this.Metadata.Equals(other.Metadata);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BaggageEntry other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (this.Value.GetHashCode() * 397) ^ (this.Metadata?.GetHashCode() ?? 0);
        }
    }
}
