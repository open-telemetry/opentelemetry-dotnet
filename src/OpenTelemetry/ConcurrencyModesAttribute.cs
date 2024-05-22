// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry;

/// <summary>
/// An attribute for declaring the supported <see cref="ConcurrencyModes"/> of an OpenTelemetry extension.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ConcurrencyModesAttribute : Attribute
{
    private readonly ConcurrencyModes supportedConcurrencyModes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyModesAttribute"/> class.
    /// </summary>
    /// <param name="supported"><see cref="ConcurrencyModes"/>.</param>
    public ConcurrencyModesAttribute(ConcurrencyModes supported)
    {
        this.supportedConcurrencyModes = supported;
    }

    /// <summary>
    /// Gets the supported <see cref="ConcurrencyModes"/>.
    /// </summary>
    public ConcurrencyModes Supported => this.supportedConcurrencyModes;
}
