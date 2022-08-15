// <copyright file="OpenTelemetryHostExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Extensions.Hosting.Implementation;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for using OpenTelemetry in an <see cref="IHost" /> application to collect telemetry.
/// </summary>
public static class OpenTelemetryHostExtensions
{
    /// <summary>
    /// Register and automatically start services required to collect OpenTelemetry traces.
    /// </summary>
    /// <param name="hostBuilder"><see cref="IHostBuilder"/>.</param>
    /// <returns>Supplied <see cref="IHostBuilder"/> for chaining calls.</returns>
    public static IHostBuilder UseOpenTelemetryTracing(this IHostBuilder hostBuilder)
        => UseOpenTelemetryTracing(hostBuilder, (b) => { });

    /// <summary>
    /// Register and automatically start services required to collect OpenTelemetry traces.
    /// </summary>
    /// <param name="hostBuilder"><see cref="IHostBuilder"/>.</param>
    /// <param name="configure">Callback action to configure the <see cref="TracerProviderBuilder"/>.</param>
    /// <returns>Supplied <see cref="IHostBuilder"/> for chaining calls.</returns>
    public static IHostBuilder UseOpenTelemetryTracing(this IHostBuilder hostBuilder, Action<TracerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(hostBuilder);

        return hostBuilder.ConfigureServices((hostBuilderContext, services) =>
        {
            services.AddOpenTelemetryTracing(configure);

            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, TelemetryHostedService>());
        });
    }

    /// <summary>
    /// Start OpenTelemetry tracing and/or metric collection.
    /// </summary>
    /// <param name="host"><see cref="IHost"/>.</param>
    /// <returns>Supplied <see cref="IHost"/> for chaining calls.</returns>
    public static IHost UseOpenTelemetry(this IHost host)
    {
        Guard.ThrowIfNull(host);

        TelemetryHostedService.Initialize(host.Services);

        return host;
    }
}
