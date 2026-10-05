// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Configuration.Declarative;
using OpenTelemetry.Internal;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for reading OpenTelemetry declarative configuration from an <see cref="IServiceProvider"/>.
/// </summary>
public static class DeclarativeConfigurationServiceProviderExtensions
{
    /// <summary>
    /// Returns the parsed declarative configuration document, or <see langword="null"/> when no
    /// declarative configuration is reachable from <paramref name="serviceProvider"/>.
    /// </summary>
    /// <param name="serviceProvider">The service provider to resolve from.</param>
    /// <returns>
    /// The parsed <see cref="DeclarativeConfigurationDocument"/>, or <see langword="null"/> when
    /// no declarative configuration is reachable.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="serviceProvider"/> is null.</exception>
    /// <exception cref="DeclarativeConfigurationException">
    /// A registered configuration file cannot be parsed. Subsequent calls rethrow the same
    /// exception without re-reading the file.
    /// </exception>
    public static DeclarativeConfigurationDocument? GetOpenTelemetryDeclarativeConfiguration(
        this IServiceProvider serviceProvider)
    {
        Guard.ThrowIfNull(serviceProvider);
        return DeclarativeConfigurationDocumentAccessorResolver.Find(serviceProvider)?.GetDocument();
    }
}
