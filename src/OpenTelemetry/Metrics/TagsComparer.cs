// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// Equality comparer for <see cref="Tags"/>.
/// </summary>
internal sealed class TagsComparer :
#if NET9_0_OR_GREATER
    IAlternateEqualityComparer<ReadOnlySpan<KeyValuePair<string, object?>>, Tags>,
#endif
    IEqualityComparer<Tags>
{
    public static readonly TagsComparer Instance = new();

    public bool Equals(Tags x, Tags y) => x.Equals(y);

    public int GetHashCode(Tags obj) => obj.GetHashCode();

#if NET9_0_OR_GREATER
    public bool Equals(ReadOnlySpan<KeyValuePair<string, object?>> alternate, Tags other)
        => other.Equals(alternate);

    public int GetHashCode(ReadOnlySpan<KeyValuePair<string, object?>> alternate)
        => Tags.ComputeHashCode(alternate);

    public Tags Create(ReadOnlySpan<KeyValuePair<string, object?>> alternate)
        => new(alternate.ToArray());
#endif
}
