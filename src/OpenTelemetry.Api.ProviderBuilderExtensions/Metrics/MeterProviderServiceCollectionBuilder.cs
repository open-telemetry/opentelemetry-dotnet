// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

internal sealed class MeterProviderServiceCollectionBuilder : MeterProviderBuilder, IMeterProviderBuilder
{
    public MeterProviderServiceCollectionBuilder(IServiceCollection services)
    {
        services.ConfigureOpenTelemetryMeterProvider((sp, builder) => this.Services = null);

        this.Services = services;
    }

    public IServiceCollection? Services { get; set; }

    public MeterProvider? Provider => null;

    /// <inheritdoc />
    public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        Guard.ThrowIfNull(instrumentationFactory);

        this.ConfigureBuilderInternal((sp, builder) =>
        {
            builder.AddInstrumentation(instrumentationFactory);
        });

        return this;
    }

    /// <inheritdoc />
    public override MeterProviderBuilder AddMeter(params string[] names)
    {
        Guard.ThrowIfNull(names);

        this.ConfigureBuilderInternal((sp, builder) =>
        {
            builder.AddMeter(names);
        });

        return this;
    }

    /// <inheritdoc />
    public MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
        => this.ConfigureServicesInternal(configure);

    /// <inheritdoc cref="IDeferredMeterProviderBuilder.Configure" />
    public MeterProviderBuilder ConfigureBuilder(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

    /// <inheritdoc />
    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

    private MeterProviderServiceCollectionBuilder ConfigureBuilderInternal(Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        var services = this.Services
            ?? throw new NotSupportedException("Builder cannot be configured during MeterProvider construction.");

        services.ConfigureOpenTelemetryMeterProvider(configure);

        return this;
    }

    private MeterProviderServiceCollectionBuilder ConfigureServicesInternal(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.Services
            ?? throw new NotSupportedException("Services cannot be configured during MeterProvider construction.");

        configure(services);

        return this;
    }
}
