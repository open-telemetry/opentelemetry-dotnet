// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// A class containing options for configuring a <see cref="Logger"/>.
/// </summary>
/// <remarks><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</remarks>
[Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
public
#else
/// <summary>
/// A class containing options for configuring a <see cref="Logger"/>.
/// </summary>
internal
#endif
class LoggerOptions
{
    /// <summary>
    /// Gets or sets the optional logger name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the optional logger version.
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Gets or sets the optional logger schema URL.
    /// </summary>
#pragma warning disable CA1056 // URI-like properties should not be strings
    public string? SchemaUrl { get; set; }
#pragma warning restore CA1056 // URI-like properties should not be strings
}
