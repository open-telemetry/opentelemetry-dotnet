// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// Wraps string value of metadata, as required by the spec: https://github.com/open-telemetry/opentelemetry-specification/blob/815598814f3cf461ad5493ccbddd53633fb5cf24/specification/baggage/api.md?plain=1#L117-L119.
/// </summary>
public readonly struct BaggageEntryMetadata : IEquatable<BaggageEntryMetadata>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaggageEntryMetadata"/> struct.
    /// </summary>
    /// <param name="value">Metadata value.</param>
    public BaggageEntryMetadata(string? value)
    {
        this.Value = value;
    }

    /// <summary>
    /// Gets the value.
    /// </summary>
    public string? Value { get; }

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public static bool operator ==(BaggageEntryMetadata left, BaggageEntryMetadata right) => left.Equals(right);

    public static bool operator !=(BaggageEntryMetadata left, BaggageEntryMetadata right) => !left.Equals(right);
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <inheritdoc />
    public bool Equals(BaggageEntryMetadata other) => string.Equals(this.Value, other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BaggageEntryMetadata other && this.Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => this.Value?.GetHashCode() ?? 0;
}
