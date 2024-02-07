// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="TracerProvider"/> and/or <see
    /// cref="MeterProvider"/> will be created for a given <see
    /// cref="IServiceCollection"/>.</item>
    /// <item>OpenTelemetry SDK services are inserted at the beginning of the
    /// <see cref="IServiceCollection"/> and started with the host. For details
    /// about the ordering of events and capturing telemetry in
    /// <see cref="IHostedService" />s see: <see href="https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Extensions.Hosting/README.md#hosted-service-ordering-and-telemetry-capture" />.</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
#pragma warning disable CS0618 // Type or member is obsolete
    // Note: OpenTelemetryBuilder is obsolete because users should target
    // IOpenTelemetryBuilder for extensions but this method is valid and
    // expected to be called to obtain a root builder.
    public static OpenTelemetryBuilder AddOpenTelemetry(this IServiceCollection services)
#pragma warning restore CS0618 // Type or member is obsolete
    {
        Guard.ThrowIfNull(services);

        if (!services.Any((ServiceDescriptor d) => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(TelemetryHostedService)))
        {
            services.Insert(0, ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());
        }

        return new(services);
    }
}
