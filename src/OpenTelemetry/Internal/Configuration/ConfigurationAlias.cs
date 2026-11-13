// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// An <c>OTEL_*</c> variable that sets the same option as a section key.
/// </summary>
internal readonly struct ConfigurationAlias
{
    public ConfigurationAlias(
        string sectionKey,
        string variableName,
        Func<string, string>? normalizeSectionValue = null)
    {
        this.SectionKey = sectionKey;
        this.VariableName = variableName;
        this.NormalizeSectionValue = normalizeSectionValue;
    }

    /// <summary>
    /// Gets the key, relative to the section, of the option the variable sets.
    /// </summary>
    public string SectionKey { get; }

    /// <summary>
    /// Gets the variable name.
    /// </summary>
    public string VariableName { get; }

    /// <summary>
    /// Gets a conversion of a section value to the form the configuration binder accepts, or
    /// <see langword="null"/> when section values are bound unchanged.
    /// </summary>
    public Func<string, string>? NormalizeSectionValue { get; }
}
