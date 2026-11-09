// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

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
    internal static string Index(string parent, int index) => $"{parent}[{index.ToString(CultureInfo.InvariantCulture)}]";

    /// <summary>
    /// Formats a document path and position for use at the start of a message.
    /// </summary>
    /// <param name="path">The document path.</param>
    /// <param name="position">The position, which may be unknown.</param>
    /// <returns>The quoted path, followed by the line and column when the position is known.</returns>
    internal static string Describe(string path, ConfigValuePosition position) =>
        position.HasPosition
            ? FormattableString.Invariant($"'{path}' (line {position.Line}, column {position.Column})")
            : $"'{path}'";
}
