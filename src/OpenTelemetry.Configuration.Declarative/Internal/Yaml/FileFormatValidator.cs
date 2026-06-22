// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// DeclarativeConfigurationException carries the OTEL1006 experimental attribute.
// Suppress once here rather than at every throw site.
#pragma warning disable OTEL1006

using System.Globalization;
using System.Text.RegularExpressions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Validates the <c>file_format</c> field of a declarative-configuration document.
/// </summary>
internal static partial class FileFormatValidator
{
    /// <summary>
    /// The major version this implementation supports.
    /// </summary>
    internal const int SupportedMajorVersion = 1;

    /// <summary>
    /// The highest minor version this implementation has been built against.
    /// </summary>
    internal const int MaxSupportedMinorVersion = 1;

    // Structural pattern: major.minor with optional -rc.N suffix.
    // Range check is done in code after parsing.
    private const string FormatPatternString = @"^(\d+)\.(\d+)(-rc\.\d+)?$";

#if !NET
    private static readonly Regex FormatPatternInstance = new(
        FormatPatternString,
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeout: TimeSpan.FromSeconds(1));
#endif

    /// <summary>
    /// Validates <paramref name="fileFormat"/> and returns the trimmed, accepted value.
    /// </summary>
    /// <param name="fileFormat">The value of the <c>file_format</c> YAML field.</param>
    /// <param name="warn">Called with a warning message when the format is accepted but has a compatibility concern.</param>
    /// <returns>The trimmed, validated <c>file_format</c> value.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when <paramref name="fileFormat"/> is null, whitespace, structurally invalid,
    /// or has an unsupported major version.
    /// </exception>
    internal static string Validate(string? fileFormat, Action<string> warn)
    {
        Guard.ThrowIfNull(warn);

        if (string.IsNullOrWhiteSpace(fileFormat))
        {
            throw new DeclarativeConfigurationException(
                $"Declarative configuration requires a 'file_format' field. " +
                $"Supported versions are '{SupportedMajorVersion}.0' through '{SupportedMajorVersion}.{MaxSupportedMinorVersion}' " +
                $"(for example: file_format: \"{SupportedMajorVersion}.0\").");
        }

        fileFormat = fileFormat!.Trim();

        var match = GetFormatPattern().Match(fileFormat);
        if (!match.Success
            || !int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor))
        {
            throw new DeclarativeConfigurationException(
                $"Unsupported file_format '{fileFormat}'. " +
                $"Expected a version of the form '{SupportedMajorVersion}.minor' " +
                $"(for example: \"{SupportedMajorVersion}.{MaxSupportedMinorVersion}\").");
        }

        if (major != SupportedMajorVersion)
        {
            throw new DeclarativeConfigurationException(
                $"Unsupported file_format '{fileFormat}': major version {major} is not supported " +
                $"(this implementation supports major version {SupportedMajorVersion}).");
        }

        // Minor is newer than this SDK knows about: accept but warn (some features may not take effect).
        if (minor > MaxSupportedMinorVersion)
        {
            warn($"Configuration file_format '{fileFormat}' is newer than the maximum version " +
                 $"supported by this SDK implementation ({SupportedMajorVersion}.{MaxSupportedMinorVersion}). " +
                 $"Features introduced in newer minor versions may not take effect.");
        }

        return fileFormat;
    }

#if NET
    [GeneratedRegex(FormatPatternString, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GetFormatPattern();
#else
    private static Regex GetFormatPattern() => FormatPatternInstance;
#endif
}
