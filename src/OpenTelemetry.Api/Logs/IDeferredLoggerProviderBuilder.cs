// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Logs;

/// <summary>
/// Describes a logger provider builder that supports deferred
/// initialization using an <see cref="IServiceProvider"/> to perform
/// dependency injection.
/// </summary>
public interface IDeferredLoggerProviderBuilder
{
    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="LoggerProviderBuilder"/> once the application <see
    /// cref="IServiceProvider"/> is available.
    /// </summary>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    LoggerProviderBuilder Configure(Action<IServiceProvider, LoggerProviderBuilder> configure);
}
