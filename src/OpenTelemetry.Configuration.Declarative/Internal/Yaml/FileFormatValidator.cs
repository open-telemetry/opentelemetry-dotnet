// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// DeclarativeConfigurationException carries the OTEL1006 experimental attribute.
// Suppress once here rather than at every throw site.
#pragma warning disable OTEL1006

using System.Text.RegularExpressions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Validates the <c>file_format</c> field of a declarative-configuration document.
/// </summary>
internal static partial class FileFormatValidator
{
    /// <summary>
    /// The exact <c>file_format</c> value this implementation targets.
    /// </summary>
    internal const string ExpectedFileFormat = "1.0";

    // Accepts: 0.4, 1.0, 1.0-rc.N.
    private const string SupportedFormatsPatternString = @"^(0\.4|1\.0(-rc\.\d+)?)$";

#if !NET8_0_OR_GREATER
    private static readonly Regex SupportedFormatsPatternInstance = new(
        SupportedFormatsPatternString,
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        matchTimeout: TimeSpan.FromSeconds(1));
#endif

    /// <summary>
    /// Validates <paramref name="fileFormat"/> and returns the trimmed, accepted value.
    /// </summary>
    /// <param name="fileFormat">The value of the <c>file_format</c> YAML field.</param>
    /// <param name="warn">Called with a warning message when the format is supported but not the expected version.</param>
    /// <returns>The trimmed, validated <c>file_format</c> value.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// Thrown when <paramref name="fileFormat"/> is null, whitespace, or not in the supported allow-list.
    /// </exception>
    internal static string Validate(string? fileFormat, Action<string> warn)
    {
        Guard.ThrowIfNull(warn);

        if (string.IsNullOrWhiteSpace(fileFormat))
        {
            throw new DeclarativeConfigurationException(
                "Declarative configuration requires a 'file_format' field (for example: file_format: \"1.0\").");
        }

        fileFormat = fileFormat!.Trim();

        if (!GetSupportedFormatsPattern().IsMatch(fileFormat))
        {
            throw new DeclarativeConfigurationException(
                $"Unsupported file_format '{fileFormat}'. Supported formats are: 0.4, 1.0, 1.0-rc.N (where N are the digits for the RC version).");
        }

        if (!string.Equals(fileFormat, ExpectedFileFormat, StringComparison.Ordinal))
        {
            warn(
                $"Configuration file_format '{fileFormat}' does not exactly match the expected version " +
                $"'{ExpectedFileFormat}'. This may result in unexpected behavior for experimental properties.");
        }

        return fileFormat;
    }

#if NET8_0_OR_GREATER
    [GeneratedRegex(SupportedFormatsPatternString, RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex GetSupportedFormatsPattern();
#else
    private static Regex GetSupportedFormatsPattern() => SupportedFormatsPatternInstance;
#endif
}
