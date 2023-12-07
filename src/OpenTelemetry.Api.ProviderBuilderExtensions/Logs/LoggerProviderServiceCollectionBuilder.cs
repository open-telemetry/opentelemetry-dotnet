// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

internal sealed class LoggerProviderServiceCollectionBuilder : LoggerProviderBuilder, ILoggerProviderBuilder
{
    public LoggerProviderServiceCollectionBuilder(IServiceCollection services)
    {
        services.ConfigureOpenTelemetryLoggerProvider((sp, builder) => this.Services = null);

        this.Services = services;
    }

    public IServiceCollection? Services { get; set; }

    public LoggerProvider? Provider => null;

    /// <inheritdoc />
    public override LoggerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        Guard.ThrowIfNull(instrumentationFactory);

        this.ConfigureBuilderInternal((sp, builder) =>
        {
            builder.AddInstrumentation(instrumentationFactory);
        });

        return this;
    }

    /// <inheritdoc />
    public LoggerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
        => this.ConfigureServicesInternal(configure);

    /// <inheritdoc cref="IDeferredLoggerProviderBuilder.Configure" />
    public LoggerProviderBuilder ConfigureBuilder(Action<IServiceProvider, LoggerProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

    /// <inheritdoc />
    LoggerProviderBuilder IDeferredLoggerProviderBuilder.Configure(Action<IServiceProvider, LoggerProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

    private LoggerProviderServiceCollectionBuilder ConfigureBuilderInternal(Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        var services = this.Services
            ?? throw new NotSupportedException("Builder cannot be configured during LoggerProvider construction.");

        services.ConfigureOpenTelemetryLoggerProvider(configure);

        return this;
    }

    private LoggerProviderServiceCollectionBuilder ConfigureServicesInternal(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.Services
            ?? throw new NotSupportedException("Services cannot be configured during LoggerProvider construction.");

        configure(services);

        return this;
    }
}
