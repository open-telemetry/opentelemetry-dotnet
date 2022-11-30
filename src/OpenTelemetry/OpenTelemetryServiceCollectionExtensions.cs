// <copyright file="OpenTelemetryServiceCollectionExtensions.cs" company="OpenTelemetry Authors">
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

#nullable enable

using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Contains <see cref="IServiceCollection"/> extension methods for registering OpenTelemetry SDK artifacts.
/// </summary>
public static class OpenTelemetryServiceCollectionExtensions
{
    /// <summary>
    /// Adds the OpenTelemetry SDK <see cref="TracerProvider"/> implementation
    /// into the supplied <see cref="IServiceCollection"/>.
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
    /// This method is provided to support hosting infrastructure and is not
    /// typically called by users.
    /// <list type="bullet">
    /// <item>Application authors should call the
    /// <c>IServiceCollection.AddOpenTelemetryTracing</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package.</item>
    /// <item>Library authors should call the <see
    /// cref="OpenTelemetryDependencyInjectionTracingServiceCollectionExtensions.ConfigureOpenTelemetryTracing(IServiceCollection)"/>
    /// extension.</item>
    /// </list>
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="TracerProvider"/>.</param>
    /// <returns>Returns the supplied <see cref="TracerProvider"/> for call
    /// chaining.</returns>
    public static IServiceCollection AddOpenTelemetryTracerProvider(this IServiceCollection services)
    {
        TracerProviderBuilderBase.RegisterTracerProvider(services);

        return services;
    }

    /// <summary>
    /// Adds the OpenTelemetry SDK <see cref="MeterProvider"/> implementation
    /// into the supplied <see cref="IServiceCollection"/>.
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
    /// This method is provided to support hosting infrastructure and is not
    /// typically called by users.
    /// <list type="bullet">
    /// <item>Application authors should call the
    /// <c>IServiceCollection.AddOpenTelemetryMetrics</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package.</item>
    /// <item>Library authors should call the <see
    /// cref="OpenTelemetryDependencyInjectionMetricsServiceCollectionExtensions.ConfigureOpenTelemetryMetrics(IServiceCollection)"/>
    /// extension.</item>
    /// </list>
    /// </item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="MeterProvider"/>.</param>
    /// <returns>Returns the supplied <see cref="MeterProvider"/> for call
    /// chaining.</returns>
    public static IServiceCollection AddOpenTelemetryMeterProvider(this IServiceCollection services)
    {
        MeterProviderBuilderBase.RegisterMeterProvider(services);

        return services;
    }
}
