// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Metrics;

/// <summary>
/// An attribute for declaring the supported <see cref="ExportModes"/> of a metric exporter.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ExportModesAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ExportModesAttribute"/> class.
    /// </summary>
    /// <param name="supported"><see cref="ExportModes"/>.</param>
    public ExportModesAttribute(ExportModes supported)
    {
        this.Supported = supported;
    }

    /// <summary>
    /// Gets the supported <see cref="ExportModes"/>.
    /// </summary>
    public ExportModes Supported { get; }
}
