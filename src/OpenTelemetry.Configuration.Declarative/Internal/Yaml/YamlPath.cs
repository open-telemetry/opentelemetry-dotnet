// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Builds paths used to identify a node in diagnostic and exception messages.
/// </summary>
internal static class YamlPath
{
    /// <summary>
    /// The path of the document root.
    /// </summary>
    internal const string Root = "<root>";

    /// <summary>
    /// Returns the path of the <paramref name="key"/> property of <paramref name="parent"/>.
    /// </summary>
    /// <param name="parent">The path of the mapping that holds the property.</param>
    /// <param name="key">The property name.</param>
    /// <returns>The composed path.</returns>
    internal static string Child(string parent, string key) => $"{parent}.{key}";

    /// <summary>
    /// Returns the path of the item at <paramref name="index"/> of <paramref name="parent"/>.
    /// </summary>
    /// <param name="parent">The path of the sequence that holds the item.</param>
    /// <param name="index">The zero-based item index.</param>
    /// <returns>The composed path.</returns>
    internal static string Index(string parent, int index) => $"{parent}[{index}]";
}
