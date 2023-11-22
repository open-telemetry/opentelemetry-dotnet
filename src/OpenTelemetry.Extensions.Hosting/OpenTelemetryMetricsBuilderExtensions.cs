// <copyright file="OpenTelemetryMetricsBuilderExtensions.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace Microsoft.Extensions.Diagnostics.Metrics;

/// <summary>
/// Contains extension methods for registering OpenTelemetry metrics with an
/// <see cref="IMetricsBuilder"/> instance.
/// </summary>
internal static class OpenTelemetryMetricsBuilderExtensions
{
    /// <summary>
    /// Adds an OpenTelemetry <see cref="IMetricsListener"/> named 'OpenTelemetry' to the <see cref="IMetricsBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="MeterProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <param name="metricsBuilder"><see cref="IMetricsBuilder"/>.</param>
    /// <returns>The supplied <see cref="IMetricsBuilder"/> for chaining
    /// calls.</returns>
    public static IMetricsBuilder UseOpenTelemetry(
        this IMetricsBuilder metricsBuilder)
        => UseOpenTelemetry(metricsBuilder, b => { });

    /// <summary>
    /// Adds an OpenTelemetry <see cref="IMetricsListener"/> named 'OpenTelemetry' to the <see cref="IMetricsBuilder"/>.
    /// </summary>
    /// <remarks><inheritdoc cref="UseOpenTelemetry(IMetricsBuilder)" path="/remarks"/></remarks>
    /// <param name="metricsBuilder"><see cref="IMetricsBuilder"/>.</param>
    /// <param name="configure"><see cref="MeterProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="IMetricsBuilder"/> for chaining
    /// calls.</returns>
    public static IMetricsBuilder UseOpenTelemetry(
        this IMetricsBuilder metricsBuilder,
        Action<MeterProviderBuilder> configure)
    {
        Guard.ThrowIfNull(metricsBuilder);

        RegisterMetricsListener(metricsBuilder.Services, configure);

        return metricsBuilder;
    }

    internal static void RegisterMetricsListener(
        IServiceCollection services,
        Action<MeterProviderBuilder> configure)
    {
        Debug.Assert(services != null, "services was null");

        Guard.ThrowIfNull(configure);

        var builder = new MeterProviderBuilderBase(services!);

        services!.TryAddEnumerable(
            ServiceDescriptor.Singleton<IMetricsListener, OpenTelemetryMetricsListener>());

        configure(builder);
    }
}
