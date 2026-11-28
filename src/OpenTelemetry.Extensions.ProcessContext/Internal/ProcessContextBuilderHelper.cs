// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Extensions.ProcessContext.Internal;

internal static class ProcessContextBuilderHelper
{
    /// <summary>
    /// Creates the instrumentation instance registered by the
    /// <c>AddProcessContext</c> extensions. Options are created per call from
    /// the provider's <see cref="IConfiguration"/> (environment variables and
    /// any other configured sources), then <paramref name="configure"/> runs,
    /// so code takes precedence over configuration.
    /// </summary>
    /// <param name="serviceProvider">The provider's service provider.</param>
    /// <param name="configure">Optional callback to configure the options.</param>
    /// <returns>The registration.</returns>
    internal static ProcessContextRegistration CreateRegistration(IServiceProvider serviceProvider, Action<ProcessContextOptions>? configure)
    {
        var options = new ProcessContextOptions(serviceProvider.GetRequiredService<IConfiguration>());
        configure?.Invoke(options);
        return new ProcessContextRegistration(options, ProcessContextRegistry.Default);
    }
}
