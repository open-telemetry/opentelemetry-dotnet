// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Describes a logger provider builder that supports deferred
/// initialization using an <see cref="IServiceProvider"/> to perform
/// dependency injection.
/// </summary>
/// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
public
#else
/// <summary>
/// Describes a logger provider builder that supports deferred
/// initialization using an <see cref="IServiceProvider"/> to perform
/// dependency injection.
/// </summary>
internal
#endif
    interface IDeferredLoggerProviderBuilder
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
