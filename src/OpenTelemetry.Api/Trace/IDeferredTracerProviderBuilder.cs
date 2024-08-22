// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Trace;

/// <summary>
/// Describes a tracer provider builder that supports deferred
/// initialization using an <see cref="IServiceProvider"/> to perform
/// dependency injection.
/// </summary>
public interface IDeferredTracerProviderBuilder
{
    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="TracerProviderBuilder"/> once the application <see
    /// cref="IServiceProvider"/> is available.
    /// </summary>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    TracerProviderBuilder Configure(Action<IServiceProvider, TracerProviderBuilder> configure);
}
