// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Reflection;

namespace OpenTelemetry;

/// <summary>
/// An attribute for declaring the supported <see cref="ConcurrencyModes"/> of an OpenTelemetry component.
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

    internal static ConcurrencyModes GetConcurrencyModeForExporter<T>(
        BaseExporter<T> exporter)
        where T : class
    {
        Debug.Assert(exporter != null, "exporter was null");

        var concurrencyMode = exporter!
            .GetType()
            .GetCustomAttribute<ConcurrencyModesAttribute>(inherit: true)?.Supported
            ?? ConcurrencyModes.Reentrant;

        if (!concurrencyMode.HasFlag(ConcurrencyModes.Reentrant))
        {
            throw new NotSupportedException("Non-reentrant simple export processors are not currently supported.");
        }

        return concurrencyMode;
    }
}
