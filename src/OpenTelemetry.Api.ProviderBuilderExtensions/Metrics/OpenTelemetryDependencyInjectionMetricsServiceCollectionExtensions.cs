// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Extension methods for setting up OpenTelemetry Metrics services in an <see cref="IServiceCollection" />.
/// </summary>
public static class OpenTelemetryDependencyInjectionMetricsServiceCollectionExtensions
{
    /// <summary>
    /// Registers an action used to configure the OpenTelemetry <see
    /// cref="MeterProviderBuilder"/>.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied
    /// sequentially.</item>
    /// <item>A <see cref="MeterProvider"/> will NOT be created automatically
    /// using this method. To begin collecting metrics use the
    /// <c>IServiceCollection.AddOpenTelemetry</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package.</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection" />.</param>
    /// <param name="configure">Callback action to configure the <see
    /// cref="MeterProviderBuilder"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls
    /// can be chained.</returns>
    public static IServiceCollection ConfigureOpenTelemetryMeterProvider(
        this IServiceCollection services,
        Action<MeterProviderBuilder> configure)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(configure);

        configure(new MeterProviderServiceCollectionBuilder(services));

        return services;
    }

    /// <summary>
    /// Registers an action used to configure the OpenTelemetry <see
    /// cref="MeterProviderBuilder"/> once the <see cref="IServiceProvider"/>
    /// is available.
    /// </summary>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied
    /// sequentially.</item>
    /// <item>A <see cref="MeterProvider"/> will NOT be created automatically
    /// using this method. To begin collecting metrics use the
    /// <c>IServiceCollection.AddOpenTelemetry</c> extension in the
    /// <c>OpenTelemetry.Extensions.Hosting</c> package.</item>
    /// <item>The supplied configuration delegate is called once the <see
    /// cref="IServiceProvider"/> is available. Services may NOT be added to a
    /// <see cref="MeterProviderBuilder"/> once the <see
    /// cref="IServiceProvider"/> has been created. Many helper extensions
    /// register services and may throw if invoked inside the configuration
    /// delegate. If you don't need access to the <see cref="IServiceProvider"/>
    /// call <see cref="ConfigureOpenTelemetryMeterProvider(IServiceCollection,
    /// Action{MeterProviderBuilder})"/> instead which is safe to be used with
    /// helper extensions.</item>
    /// </list>
    /// </remarks>
    /// <param name="services"><see cref="IServiceCollection" />.</param>
    /// <param name="configure">Callback action to configure the <see
    /// cref="MeterProviderBuilder"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> so that additional calls
    /// can be chained.</returns>
    public static IServiceCollection ConfigureOpenTelemetryMeterProvider(
        this IServiceCollection services,
        Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        Guard.ThrowIfNull(services);
        Guard.ThrowIfNull(configure);

        services.AddSingleton<IConfigureMeterProviderBuilder>(
            new ConfigureMeterProviderBuilderCallbackWrapper(configure));

        return services;
    }

    private sealed class ConfigureMeterProviderBuilderCallbackWrapper : IConfigureMeterProviderBuilder
    {
        private readonly Action<IServiceProvider, MeterProviderBuilder> configure;

        public ConfigureMeterProviderBuilderCallbackWrapper(Action<IServiceProvider, MeterProviderBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            this.configure = configure;
        }

        public void ConfigureBuilder(IServiceProvider serviceProvider, MeterProviderBuilder meterProviderBuilder)
        {
            this.configure(serviceProvider, meterProviderBuilder);
        }
    }
}
