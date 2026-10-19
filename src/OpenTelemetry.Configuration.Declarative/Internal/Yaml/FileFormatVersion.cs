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
    /// Gets a value indicating whether the major version is the one this package supports.
    /// </summary>
    internal bool IsSupportedMajorVersion => this.Major == FileFormatValidator.SupportedMajorVersion;

    /// <summary>
    /// Gets a value indicating whether the version is a newer minor version of the supported major
    /// version than the schema this package understands.
    /// </summary>
    internal bool IsNewerMinorVersion =>
        this.IsSupportedMajorVersion && this.Minor > FileFormatValidator.MaxSupportedMinorVersion;
}
