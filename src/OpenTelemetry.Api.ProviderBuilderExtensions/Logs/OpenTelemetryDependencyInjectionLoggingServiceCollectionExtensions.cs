// <copyright file="OpenTelemetryDependencyInjectionLoggingServiceCollectionExtensions.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs;

/// <summary>
/// Extension methods for setting up OpenTelemetry logging services in an <see cref="IServiceCollection" />.
/// </summary>
#if EXPOSE_EXPERIMENTAL_FEATURES
public
#else
    internal
#endif
static class OpenTelemetryDependencyInjectionLoggingServiceCollectionExtensions
{
#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Registers an action used to configure the OpenTelemetry <see
    /// cref="LoggerProviderBuilder"/>.
    /// </summary>
    /// <remarks>
    /// <para><inheritdoc cref="OpenTelemetryDependencyInjectionLoggerProviderBuilderExtensions.AddInstrumentation{T}(LoggerProviderBuilder, T)" path="/remarks"/></para>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied
    /// sequentially.</item>
    /// <item>A <see cref="LoggerProvider"/> will NOT be created automatically
    /// using this method. To begin collecting logs use the
    /// <c>IServiceCollection.AddOpenTelemetry</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package.</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection" />.</param>
    /// <param name="configure">Callback action to configure the <see
    /// cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls
    /// can be chained.</returns>
#else
    /// <summary>
    /// Registers an action used to configure the OpenTelemetry <see
    /// cref="LoggerProviderBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied
    /// sequentially.</item>
    /// <item>A <see cref="LoggerProvider"/> will NOT be created automatically
    /// using this method. To begin collecting logs use the
    /// <c>IServiceCollection.AddOpenTelemetry</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package.</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection" />.</param>
    /// <param name="configure">Callback action to configure the <see
    /// cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls
    /// can be chained.</returns>
#endif
    public static IServiceCollection ConfigureOpenTelemetryLoggerProvider(
        this IServiceCollection services,
        Action<LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(configure);

        configure(new LoggerProviderServiceCollectionBuilder(services));

        return services;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Registers an action used to configure the OpenTelemetry <see
    /// cref="LoggerProviderBuilder"/> once the <see cref="IServiceProvider"/>
    /// is available.
    /// </summary>
    /// <remarks>
    /// <para><inheritdoc cref="OpenTelemetryDependencyInjectionLoggerProviderBuilderExtensions.AddInstrumentation{T}(LoggerProviderBuilder, T)" path="/remarks"/></para>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied
    /// sequentially.</item>
    /// <item>A <see cref="LoggerProvider"/> will NOT be created automatically
    /// using this method. To begin collecting logs use the
    /// <c>IServiceCollection.AddOpenTelemetry</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package.</item>
    /// <item>The supplied configuration delegate is called once the <see
    /// cref="IServiceProvider"/> is available. Services may NOT be added to a
    /// <see cref="LoggerProviderBuilder"/> once the <see
    /// cref="IServiceProvider"/> has been created. Many helper extensions
    /// register services and may throw if invoked inside the configuration
    /// delegate. If you don't need access to the <see cref="IServiceProvider"/>
    /// call <see cref="ConfigureOpenTelemetryLoggerProvider(IServiceCollection,
    /// Action{LoggerProviderBuilder})"/> instead which is safe to be used with
    /// helper extensions.</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection" />.</param>
    /// <param name="configure">Callback action to configure the <see
    /// cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls
    /// can be chained.</returns>
#else
    /// <summary>
    /// Registers an action used to configure the OpenTelemetry <see
    /// cref="LoggerProviderBuilder"/> once the <see cref="IServiceProvider"/>
    /// is available.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied
    /// sequentially.</item>
    /// <item>A <see cref="LoggerProvider"/> will NOT be created automatically
    /// using this method. To begin collecting logs use the
    /// <c>IServiceCollection.AddOpenTelemetry</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package.</item>
    /// <item>The supplied configuration delegate is called once the <see
    /// cref="IServiceProvider"/> is available. Services may NOT be added to a
    /// <see cref="LoggerProviderBuilder"/> once the <see
    /// cref="IServiceProvider"/> has been created. Many helper extensions
    /// register services and may throw if invoked inside the configuration
    /// delegate. If you don't need access to the <see cref="IServiceProvider"/>
    /// call <see cref="ConfigureOpenTelemetryLoggerProvider(IServiceCollection,
    /// Action{LoggerProviderBuilder})"/> instead which is safe to be used with
    /// helper extensions.</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection" />.</param>
    /// <param name="configure">Callback action to configure the <see
    /// cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls
    /// can be chained.</returns>
#endif
    public static IServiceCollection ConfigureOpenTelemetryLoggerProvider(
        this IServiceCollection services,
        Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(configure);

        services.AddSingleton<IConfigureLoggerProviderBuilder>(
            new ConfigureLoggerProviderBuilderCallbackWrapper(configure));

        return services;
    }

    private sealed class ConfigureLoggerProviderBuilderCallbackWrapper : IConfigureLoggerProviderBuilder
    {
        private readonly Action<IServiceProvider, LoggerProviderBuilder> configure;

        public ConfigureLoggerProviderBuilderCallbackWrapper(Action<IServiceProvider, LoggerProviderBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            this.configure = configure;
        }

        public void ConfigureBuilder(IServiceProvider serviceProvider, LoggerProviderBuilder loggerProviderBuilder)
        {
            this.configure(serviceProvider, loggerProviderBuilder);
        }
    }
}
