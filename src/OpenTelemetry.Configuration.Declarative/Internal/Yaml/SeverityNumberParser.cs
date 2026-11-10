// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Collections.Frozen;
#endif
using YamlDotNet.Core;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Parses the schema's lowercase severity names, as used by <c>log_level</c>, into <see cref="SeverityNumber"/>.
/// </summary>
internal static class SeverityNumberParser
{
#if NET
    private static readonly SeverityNumber[] Values = Enum.GetValues<SeverityNumber>();
#else
    private static readonly SeverityNumber[] Values = (SeverityNumber[])Enum.GetValues(typeof(SeverityNumber));
#endif

#if NET
    private static readonly FrozenDictionary<string, SeverityNumber> Severities = BuildSeverities();
#else
    private static readonly Dictionary<string, SeverityNumber> Severities = BuildSeverities();
#endif

    private static readonly string AllowedValues = string.Join(", ", Values.Select(ToName));

    /// <summary>
    /// Parses a severity name.
    /// </summary>
    /// <param name="key">The key the value belongs to, for the error message.</param>
    /// <param name="text">The value as written in the document.</param>
    /// <param name="position">The position of the value, for the error message.</param>
    /// <returns>The severity.</returns>
    /// <exception cref="DeclarativeConfigurationException">
    /// The value is not one of the schema's lowercase severity names.
    /// </exception>
    internal static SeverityNumber Parse(string key, string text, Mark position) =>
        Severities.TryGetValue(text, out var severity)
            ? severity
            : throw new DeclarativeConfigurationException(
                $"'{key}' is '{text}' {Describe(position)}. {CaseHint(text)}Allowed values are: {AllowedValues}.");

    /// <summary>
    /// Creates the exception for a severity value that is not a string.
    /// </summary>
    /// <param name="key">The key the value belongs to.</param>
    /// <param name="actual">A description of what the value is.</param>
    /// <param name="position">The position of the value.</param>
    /// <returns>The exception to throw.</returns>
    internal static DeclarativeConfigurationException CreateTypeMismatch(string key, string actual, Mark position) =>
        new($"'{key}' {Describe(position)} must be a string, but is a {actual} value. Allowed values are: {AllowedValues}.");

    private static string Describe(Mark position) =>
        FormattableString.Invariant($"at line {position.Line}, column {position.Column}");

    private static string CaseHint(string text)
    {
#pragma warning disable CA1308 // The schema's values are lowercase, so lowercase is the normal form to suggest.
        var lower = text.ToLowerInvariant();
#pragma warning restore CA1308
        return !string.Equals(lower, text, StringComparison.Ordinal) && Severities.ContainsKey(lower)
            ? $"Values are lowercase: did you mean '{lower}'? If the value comes from ${{OTEL_LOG_LEVEL}}, set that variable in lowercase. "
            : string.Empty;
    }

#pragma warning disable CA1308 // The schema's values are lowercase.
    private static string ToName(SeverityNumber severity) => severity.ToString().ToLowerInvariant();
#pragma warning restore CA1308

    private static Dictionary<string, SeverityNumber> BuildSeverityDictionary()
    {
        var severities = new Dictionary<string, SeverityNumber>(Values.Length, StringComparer.Ordinal);

        foreach (var severity in Values)
        {
            severities[ToName(severity)] = severity;
        }

        return severities;
    }

#if NET
    private static FrozenDictionary<string, SeverityNumber> BuildSeverities() =>
        BuildSeverityDictionary().ToFrozenDictionary(StringComparer.Ordinal);
#else
    private static Dictionary<string, SeverityNumber> BuildSeverities() => BuildSeverityDictionary();
#endif
}
