// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using YamlDotNet.RepresentationModel;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Compares <see cref="YamlNode"/> instances by reference.
/// </summary>
/// <remarks>
/// <see cref="YamlNode"/> implements structural equality, so two distinct nodes that happen to hold
/// the same content compare equal. Anything keyed on node identity - such as per-node caching or
/// alias-cycle detection - needs reference semantics instead.
/// </remarks>
internal sealed class YamlNodeReferenceEqualityComparer : IEqualityComparer<YamlNode>
{
    /// <summary>
    /// The shared instance.
    /// </summary>
    internal static readonly YamlNodeReferenceEqualityComparer Instance = new();

    private YamlNodeReferenceEqualityComparer()
    {
    }

    bool IEqualityComparer<YamlNode>.Equals(YamlNode? x, YamlNode? y) => ReferenceEquals(x, y);

    int IEqualityComparer<YamlNode>.GetHashCode(YamlNode obj) => RuntimeHelpers.GetHashCode(obj);
}
