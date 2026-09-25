// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Configuration.Declarative;
using OpenTelemetry.Internal;

namespace Microsoft.Extensions.Configuration;

/// <summary>
/// Extension methods for reading OpenTelemetry declarative configuration from an <see cref="IConfigurationRoot"/>.
/// </summary>
public static class DeclarativeConfigurationExtensions
{
    /// <summary>
    /// Returns the parsed declarative configuration document, or <see langword="null"/> when no
    /// declarative configuration is reachable from <paramref name="configuration"/>.
    /// </summary>
    /// <remarks>
    /// A <see cref="ConfigurationManager"/> implements <see cref="IConfigurationRoot"/>, so this
    /// overload can be used at registration time before a service provider exists.
    /// </remarks>
    /// <param name="configuration">The configuration root to scan.</param>
    /// <returns>
    /// The parsed <see cref="DeclarativeConfigurationDocument"/>, or <see langword="null"/> when
    /// no declarative configuration is reachable.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="configuration"/> is null.</exception>
    /// <exception cref="DeclarativeConfigurationException">
    /// A registered configuration file cannot be parsed. Subsequent calls rethrow the same
    /// exception without re-reading the file.
    /// </exception>
    public static DeclarativeConfigurationDocument? GetOpenTelemetryDeclarativeConfiguration(
        this IConfigurationRoot configuration)
    {
        Guard.ThrowIfNull(configuration);
        return DeclarativeConfigurationDocumentAccessorResolver.FindInConfiguration(configuration)?.GetDocument();
    }
}
