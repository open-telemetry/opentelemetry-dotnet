// <copyright file="OpenTelemetryDependencyInjectionTracingServiceCollectionExtensions.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Extension methods for setting up OpenTelemetry tracing services in an <see cref="IServiceCollection" />.
/// </summary>
public static class OpenTelemetryDependencyInjectionTracingServiceCollectionExtensions
{
    /// <summary>
    /// Registers an action used to configure the OpenTelemetry <see
    /// cref="TracerProviderBuilder"/> used to create the <see
    /// cref="TracerProvider"/> for the <see cref="IServiceCollection"/> being
    /// configured.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied
    /// sequentially.</item>
    /// <item>A <see cref="TracerProvider"/> will not be created automatically
    /// using this method. To begin collecting metrics use the
    /// <c>IServiceCollection.AddOpenTelemetry</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package.</item>
    /// </list>
    /// </remarks>
    /// <param name="services">The <see cref="IServiceCollection" /> to add
    /// services to.</param>
    /// <param name="configure">Callback action to configure the <see
    /// cref="TracerProviderBuilder"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls
    /// can be chained.</returns>
    public static IServiceCollection ConfigureOpenTelemetryTracerProvider(
        this IServiceCollection services,
        Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        RegisterBuildAction(services, configure);

        return services;
    }

    private static void RegisterBuildAction(IServiceCollection services, Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(configure);

        services.AddSingleton<IConfigureTracerProviderBuilder>(
            new ConfigureTracerProviderBuilderCallbackWrapper(configure));
    }

    private sealed class ConfigureTracerProviderBuilderCallbackWrapper : IConfigureTracerProviderBuilder
    {
        private readonly Action<IServiceProvider, TracerProviderBuilder> configure;

        public ConfigureTracerProviderBuilderCallbackWrapper(Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            this.configure = configure;
        }

        public void ConfigureBuilder(IServiceProvider serviceProvider, TracerProviderBuilder tracerProviderBuilder)
        {
            this.configure(serviceProvider, tracerProviderBuilder);
        }
    }
}
