// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

internal sealed class TracerProviderServiceCollectionBuilder : TracerProviderBuilder, ITracerProviderBuilder
{
    public TracerProviderServiceCollectionBuilder(IServiceCollection services)
    {
        services.ConfigureOpenTelemetryTracerProvider((sp, builder) => this.Services = null);

        this.Services = services;
    }

    public IServiceCollection? Services { get; set; }

    public TracerProvider? Provider => null;

    /// <inheritdoc />
    public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        Guard.ThrowIfNull(instrumentationFactory);

        this.ConfigureBuilderInternal((sp, builder) =>
        {
            builder.AddInstrumentation(instrumentationFactory);
        });

        return this;
    }

    /// <inheritdoc />
    public override TracerProviderBuilder AddSource(params string[] names)
    {
        Guard.ThrowIfNull(names);

        this.ConfigureBuilderInternal((sp, builder) =>
        {
            builder.AddSource(names);
        });

        return this;
    }

    /// <inheritdoc />
    public override TracerProviderBuilder AddLegacySource(string operationName)
    {
        Guard.ThrowIfNullOrWhitespace(operationName);

        this.ConfigureBuilderInternal((sp, builder) =>
        {
            builder.AddLegacySource(operationName);
        });

        return this;
    }

    /// <inheritdoc />
    public TracerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
        => this.ConfigureServicesInternal(configure);

    /// <inheritdoc cref="IDeferredTracerProviderBuilder.Configure" />
    public TracerProviderBuilder ConfigureBuilder(Action<IServiceProvider, TracerProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

    /// <inheritdoc />
    TracerProviderBuilder IDeferredTracerProviderBuilder.Configure(Action<IServiceProvider, TracerProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

    private TracerProviderServiceCollectionBuilder ConfigureBuilderInternal(Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        var services = this.Services
            ?? throw new NotSupportedException("Builder cannot be configured during TracerProvider construction.");

        services.ConfigureOpenTelemetryTracerProvider(configure);

        return this;
    }

    private TracerProviderServiceCollectionBuilder ConfigureServicesInternal(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.Services
            ?? throw new NotSupportedException("Services cannot be configured during TracerProvider construction.");

        configure(services);

        return this;
    }
}
