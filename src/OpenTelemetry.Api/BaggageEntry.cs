namespace OpenTelemetry;

/// <summary>
/// Represents a W3C Baggage list-member, consisting of a decoded value
/// and optional raw properties (metadata).
/// </summary>
/// <remarks>
/// Spec reference:
/// <a href="https://www.w3.org/TR/baggage/#list-member">W3C Baggage §3.2 list-member</a>.
/// <para>
/// The W3C grammar is:
///   list-member = key OWS "=" OWS value *( OWS ";" OWS property )
/// </para>
/// <para>
/// <see cref="Value"/> contains only the <c>value</c> portion (decoded).
/// <see cref="Metadata"/> contains the raw <c>*( OWS ";" OWS property )</c>
/// portion as a single string, or <see langword="null"/> when no properties
/// were present. Full property parsing is out of scope for this iteration;
/// see https://github.com/open-telemetry/opentelemetry-dotnet/issues/7374.
/// </para>
/// </remarks>
public readonly struct BaggageEntry : IEquatable<BaggageEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="BaggageEntry"/> struct.
    /// </summary>
    /// <param name="value">The decoded baggage value.</param>
    /// <param name="metadata">
    /// The raw property string (everything after the first semicolon),
    /// or <see langword="null"/> if no properties were present.
    /// </param>
    internal BaggageEntry(string value, string? metadata)
    {
        this.Value = value;
        this.Metadata = metadata;
    }

    /// <summary>
    /// Gets the decoded value of the baggage list-member.
    /// </summary>
    public string Value { get; }

    /// <summary>
    /// Gets the raw W3C property string for this list-member, or
    /// <see langword="null"/> if no properties were present on the wire.
    /// </summary>
    /// <remarks>
    /// For the wire entry <c>key=someValue;prop1=v1;prop2</c>, this
    /// property returns <c>prop1=v1;prop2</c> (trimmed, semicolon-delimited).
    /// </remarks>
    public string? Metadata { get; }

    /// <inheritdoc/>
    public static bool operator ==(BaggageEntry left, BaggageEntry right)
        => left.Equals(right);

    /// <inheritdoc/>
    public static bool operator !=(BaggageEntry left, BaggageEntry right)
        => !(left == right);

    /// <inheritdoc/>
    public bool Equals(BaggageEntry other)
        => string.Equals(this.Value, other.Value, StringComparison.Ordinal)
        && string.Equals(this.Metadata, other.Metadata, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is BaggageEntry other && this.Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        var hash = 17;
        unchecked
        {
            hash = (hash * 23) + (this.Value?.GetHashCode(StringComparison.Ordinal) ?? 0);
            hash = (hash * 23) + (this.Metadata?.GetHashCode(StringComparison.Ordinal) ?? 0);
        }
        return hash;
    }
}