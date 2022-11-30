// <copyright file="OpenTelemetryDependencyInjectionMetricsServiceCollectionExtensions.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for setting up OpenTelemetry Metrics services in an <see cref="IServiceCollection" />.
/// </summary>
public static class OpenTelemetryDependencyInjectionMetricsServiceCollectionExtensions
{
    /// <summary>
    /// Configures OpenTelemetry Metrics services in the supplied <see
    /// cref="IServiceCollection" />.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="MeterProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.</item>
    /// <item>A <see cref="MeterProvider"/> will not be created automatically
    /// using this method. To begin collecting metrics either use the
    /// <c>IServiceCollection.AddOpenTelemetryMetrics</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package or use the
    /// <c>IServiceCollection.AddOpenTelemetryMeterProvider</c> extension in the
    /// <c>OpenTelemetry</c> package and access the <see cref="MeterProvider"/>
    /// through the application <see cref="IServiceProvider"/>.</item>
    /// </list>
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection" /> to add
    /// services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls
    /// can be chained.</returns>
    public static IServiceCollection ConfigureOpenTelemetryMetrics(this IServiceCollection services)
        => ConfigureOpenTelemetryMetrics(services, (b) => { });

    /// <summary>
    /// Configures OpenTelemetry Metrics services in the supplied <see cref="IServiceCollection" />.
    /// </summary>
    /// <remarks><inheritdoc cref="ConfigureOpenTelemetryMetrics(IServiceCollection)" path="/remarks"/></remarks>
    /// <param name="services">The <see cref="IServiceCollection" /> to add services to.</param>
    /// <param name="configure">Callback action to configure the <see cref="MeterProviderBuilder"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls can be chained.</returns>
    public static IServiceCollection ConfigureOpenTelemetryMetrics(this IServiceCollection services, Action<MeterProviderBuilder> configure)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(configure);

        var builder = new DeferredMeterProviderBuilder(services);

        configure(builder);

        return services;
    }
}
