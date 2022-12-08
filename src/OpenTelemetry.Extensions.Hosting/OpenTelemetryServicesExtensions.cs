// <copyright file="OpenTelemetryServicesExtensions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.Hosting;
using OpenTelemetry;
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
    /// Configure OpenTelemetry and register a <see cref="IHostedService"/>
    /// to automatically start tracing services in the supplied <see
    /// cref="IServiceCollection" />.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>
    /// This is safe to be called multiple times. Only a single <see
    /// cref="TracerProvider"/> will be created for a given <see
    /// cref="IServiceCollection"/>.
    /// </item>
    /// <item>
    /// This method should be called by application host code. Library
    /// authors should call <see
    /// cref="OpenTelemetryDependencyInjectionTracingServiceCollectionExtensions.ConfigureOpenTelemetryTracerProvider(IServiceCollection, Action{IServiceProvider, TracerProviderBuilder})"/>
    /// instead.
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    /// <returns>Supplied <see cref="IServiceCollection"/> for chaining
    /// calls.</returns>
    [Obsolete("Use the AddOpenTelemetry().WithTracing(configure).StartWithHost() pattern instead. This method will be removed in a future version.")]
    public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services)
        => AddOpenTelemetryTracing(services, b => { });

    /// <summary>
    /// Configure OpenTelemetry and register a <see cref="IHostedService"/>
    /// to automatically start tracing services in the supplied <see
    /// cref="IServiceCollection" />.
    /// </summary>
    /// <remarks><inheritdoc
    /// cref="AddOpenTelemetryTracing(IServiceCollection)"
    /// path="/remarks"/></remarks>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Callback action to configure the <see
    /// cref="TracerProviderBuilder"/>.</param>
    /// <returns>Supplied <see cref="IServiceCollection"/> for chaining
    /// calls.</returns>
    [Obsolete("Use the AddOpenTelemetry().WithTracing(configure).StartWithHost() pattern instead. This method will be removed in a future version.")]
    public static IServiceCollection AddOpenTelemetryTracing(this IServiceCollection services, Action<TracerProviderBuilder> configure)
    {
        services.AddOpenTelemetry().WithTracing(configure).StartWithHost();

        return services;
    }

    /// <summary>
    /// Configure OpenTelemetry and register a <see cref="IHostedService"/>
    /// to automatically start metric services in the supplied <see
    /// cref="IServiceCollection" />.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>
    /// This is safe to be called multiple times. Only a single <see
    /// cref="MeterProvider"/> will be created for a given <see
    /// cref="IServiceCollection"/>.
    /// </item>
    /// <item>
    /// This method should be called by application host code. Library
    /// authors should call <see
    /// cref="OpenTelemetryDependencyInjectionMetricsServiceCollectionExtensions.ConfigureOpenTelemetryMeterProvider(IServiceCollection, Action{IServiceProvider, MeterProviderBuilder})"/>
    /// instead.
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    /// <returns>Supplied <see cref="IServiceCollection"/> for chaining
    /// calls.</returns>
    [Obsolete("Use the AddOpenTelemetry().WithMetrics(configure).StartWithHost() pattern instead. This method will be removed in a future version.")]
    public static IServiceCollection AddOpenTelemetryMetrics(this IServiceCollection services)
        => AddOpenTelemetryMetrics(services, b => { });

    /// <summary>
    /// Configure OpenTelemetry and register a <see cref="IHostedService"/>
    /// to automatically start metric services in the supplied <see
    /// cref="IServiceCollection" />.
    /// </summary>
    /// <remarks><inheritdoc
    /// cref="AddOpenTelemetryMetrics(IServiceCollection)"
    /// path="/remarks"/></remarks>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    /// <param name="configure">Callback action to configure the <see
    /// cref="TracerProviderBuilder"/>.</param>
    /// <returns>Supplied <see cref="IServiceCollection"/> for chaining
    /// calls.</returns>
    [Obsolete("Use the AddOpenTelemetry().WithMetrics(configure).StartWithHost() pattern instead. This method will be removed in a future version.")]
    public static IServiceCollection AddOpenTelemetryMetrics(this IServiceCollection services, Action<MeterProviderBuilder> configure)
    {
        services.AddOpenTelemetry().WithMetrics(configure).StartWithHost();

        return services;
    }
}
