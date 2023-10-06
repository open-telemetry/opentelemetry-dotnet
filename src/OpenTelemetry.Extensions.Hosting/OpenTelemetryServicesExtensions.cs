// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0r the License.
// </copyright>

using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up OpenTelemetry services in an <see
/// cref="IServiceCollection" />.
/// </summary>
public static class OpenTelemetryServicesExtensions
{
    /// <summary>
    /// Adds OpenTelemetry SDK services into the supplied <see
    /// cref="IServiceCollection"/>.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="TracerProvider"/> and/or <see
    /// cref="MeterProvider"/> will be created for a given <see
    /// cref="IServiceCollection"/>.
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static OpenTelemetryBuilder AddOpenTelemetry(this IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());

        return new(services);
    }
}
