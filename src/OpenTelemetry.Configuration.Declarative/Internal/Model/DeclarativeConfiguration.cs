// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// Typed in-memory model of a declarative-configuration document (the subset this package currently
/// supports). This is the authoritative output of parsing. The flat <c>OTEL_*</c> projection is derived
/// from it.
/// </summary>
/// <remarks>
/// This is a source-agnostic data record. It carries no knowledge of how it was produced. The YAML AST
/// walk that builds it lives in <see cref="DeclarativeConfigurationParser"/>, so a future non-YAML source
/// (for example a telemetry-policy push) could construct the same model without taking a YAML dependency.
/// </remarks>
/// <param name="FileFormat">The validated <c>file_format</c> value.</param>
internal sealed record DeclarativeConfiguration(string FileFormat)
{
    /// <summary>
    /// Gets the <c>disabled</c> flag.
    /// </summary>
    public ConfigProperty<bool> Disabled { get; init; }

    /// <summary>
    /// Gets the <c>resource</c> section.
    /// </summary>
    public ConfigProperty<ResourceConfiguration> Resource { get; init; }
}
