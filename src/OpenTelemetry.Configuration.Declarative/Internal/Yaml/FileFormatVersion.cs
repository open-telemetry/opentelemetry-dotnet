// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// A validated <c>file_format</c> value.
/// </summary>
/// <param name="Value">The value as written in the document.</param>
/// <param name="Major">The major version.</param>
/// <param name="Minor">The minor version.</param>
internal readonly record struct FileFormatVersion(string Value, int Major, int Minor)
{
    /// <summary>
    /// Gets a value indicating whether the minor version is newer than the schema this package understands.
    /// </summary>
    internal bool IsNewerMinorVersion => this.Minor > FileFormatValidator.MaxSupportedMinorVersion;
}
