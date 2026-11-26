// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Builds paths used to identify a node in diagnostic and exception messages.
/// </summary>
/// <remarks>
/// A path longer than <see cref="MaximumLength"/> is truncated and ends with <see cref="Ellipsis"/>.
/// Document walkers build a path for each collection they visit, so without the limit a long key
/// reused through an alias at every level would make each path, and the total allocation, grow with
/// the key length times the depth. Messages also give the line and column when available.
/// </remarks>
internal static class YamlPath
{
    /// <summary>
    /// The path of the document root.
    /// </summary>
    internal const string Root = "<root>";

    /// <summary>
    /// The maximum length of a path, including <see cref="Ellipsis"/> when truncated.
    /// </summary>
    internal const int MaximumLength = 1024;

    /// <summary>
    /// The suffix of a truncated path.
    /// </summary>
    internal const string Ellipsis = "...";

    /// <summary>
    /// Returns the path of the <paramref name="key"/> property of <paramref name="parent"/>.
    /// </summary>
    /// <param name="parent">
    /// The path of the mapping that holds the property, or empty for an unprefixed root path.
    /// </param>
    /// <param name="key">The property name.</param>
    /// <returns>The composed path, truncated to <see cref="MaximumLength"/>.</returns>
    internal static string Child(string parent, string key)
    {
        var separator = parent.Length == 0 ? string.Empty : ".";
        return parent.Length + separator.Length + key.Length <= MaximumLength
            ? string.Concat(parent, separator, key)
            : Truncate(parent, separator, key);
    }

    /// <summary>
    /// Returns the path of the item at <paramref name="index"/> of <paramref name="parent"/>.
    /// </summary>
    /// <param name="parent">The path of the sequence that holds the item.</param>
    /// <param name="index">The zero-based item index.</param>
    /// <returns>The composed path, truncated to <see cref="MaximumLength"/>.</returns>
    internal static string Index(string parent, int index)
    {
        var segment = $"[{index.ToString(CultureInfo.InvariantCulture)}]";
        return parent.Length + segment.Length <= MaximumLength
            ? parent + segment
            : Truncate(parent, segment, string.Empty);
    }

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

    // Copies only what fits, so the cost does not depend on the key length.
    private static string Truncate(string parent, string separator, string key)
    {
        var room = MaximumLength - Ellipsis.Length;
        var head = parent.Length > room ? parent.Substring(0, room) : parent;
        room -= head.Length;

        var separatorPart = separator.Length > room ? separator.Substring(0, room) : separator;
        room -= separatorPart.Length;

        var keyPart = key.Length > room ? key.Substring(0, room) : key;
        return string.Concat(head, separatorPart, keyPart, Ellipsis);
    }
}
