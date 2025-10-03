// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using static OpenTelemetry.OpenTelemetrySdk;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains methods for building <see cref="MeterProvider"/> instances.
/// </summary>
public class MeterProviderBuilderBase : MeterProviderBuilder, IMeterProviderBuilder
{
    private readonly bool allowBuild;
    private readonly MeterProviderServiceCollectionBuilder innerBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="MeterProviderBuilderBase"/> class.
    /// </summary>
    public MeterProviderBuilderBase()
    {
        var services = new ServiceCollection();

        services
            .AddOpenTelemetrySharedProviderBuilderServices()
            .AddOpenTelemetryMeterProviderBuilderServices()
            .TryAddSingleton<MeterProvider>(
                sp => throw new NotSupportedException("Self-contained MeterProvider cannot be accessed using the application IServiceProvider call Build instead."));

        this.innerBuilder = new MeterProviderServiceCollectionBuilder(services);

        this.allowBuild = true;
    }

    internal MeterProviderBuilderBase(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services
            .AddOpenTelemetryMeterProviderBuilderServices()
            .TryAddSingleton<MeterProvider>(sp => new MeterProviderSdk(sp, ownsServiceProvider: false));

        this.innerBuilder = new MeterProviderServiceCollectionBuilder(services);

        this.allowBuild = false;
    }

    /// <inheritdoc />
    MeterProvider? IMeterProviderBuilder.Provider => null;

    /// <inheritdoc />
    public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        this.innerBuilder.AddInstrumentation(instrumentationFactory);

        return this;
    }

    /// <inheritdoc />
    public override MeterProviderBuilder AddMeter(params string[] names)
    {
        this.innerBuilder.AddMeter(names);

        return this;
    }

    /// <inheritdoc />
    MeterProviderBuilder IMeterProviderBuilder.ConfigureServices(Action<IServiceCollection> configure)
    {
        this.innerBuilder.ConfigureServices(configure);

        return this;
    }

    /// <inheritdoc />
#pragma warning disable CA1033 // Interface methods should be callable by child types
    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
#pragma warning restore CA1033 // Interface methods should be callable by child types
    {
        this.innerBuilder.ConfigureBuilder(configure);

        return this;
    }

    internal MeterProvider InvokeBuild()
        => this.Build();

    /// <summary>
    /// Run the configured actions to initialize the <see cref="MeterProvider"/>.
    /// </summary>
    /// <returns><see cref="MeterProvider"/>.</returns>
    protected MeterProvider Build()
    {
        if (!this.allowBuild)
        {
            throw new NotSupportedException("A MeterProviderBuilder bound to external service cannot be built directly. Access the MeterProvider using the application IServiceProvider instead.");
        }

        var services = this.innerBuilder.Services
            ?? throw new NotSupportedException("MeterProviderBuilder build method cannot be called multiple times.");

        this.innerBuilder.Services = null;

#if DEBUG
        bool validateScopes = true;
#else
        bool validateScopes = false;
#endif
        var serviceProvider = services.BuildServiceProvider(validateScopes);
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        if (configuration.GetValue(SdkConfigDefinitions.SdkDisableEnvVarName, false))
        {
            serviceProvider.Dispose();
            return new NoopMeterProvider();
        }

        return new MeterProviderSdk(serviceProvider, ownsServiceProvider: true);
    }
}
