// <copyright file="MeterProviderBuilderBase.cs" company="OpenTelemetry Authors">
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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains methods for building <see cref="MeterProvider"/> instances.
/// </summary>
public class MeterProviderBuilderBase : MeterProviderBuilder, IMeterProviderBuilder
{
    private readonly bool allowBuild;
    private IServiceCollection? services;

    public MeterProviderBuilderBase()
    {
        var services = new ServiceCollection();

        services
            .AddOpenTelemetrySharedProviderBuilderServices()
            .AddOpenTelemetryMeterProviderBuilderServices()
            .TryAddSingleton<MeterProvider>(
                sp => throw new NotSupportedException("Self-contained MeterProvider cannot be accessed using the application IServiceProvider call Build instead."));

        services.ConfigureOpenTelemetryMeterProvider((sp, builder) => this.services = null);

        this.services = services;

        this.allowBuild = true;
    }

    internal MeterProviderBuilderBase(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services
            .AddOpenTelemetryMeterProviderBuilderServices()
            .TryAddSingleton<MeterProvider>(sp => new MeterProviderSdk(sp, ownsServiceProvider: false));

        services.ConfigureOpenTelemetryMeterProvider((sp, builder) => this.services = null);

        this.services = services;

        this.allowBuild = false;
    }

    /// <inheritdoc />
    MeterProvider? IMeterProviderBuilder.Provider => null;

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
    MeterProviderBuilder IMeterProviderBuilder.ConfigureServices(Action<IServiceCollection> configure)
        => this.ConfigureServicesInternal(configure);

    /// <inheritdoc />
    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

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

        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("MeterProviderBuilder build method cannot be called multiple times.");
        }

        this.services = null;

#if DEBUG
        bool validateScopes = true;
#else
        bool validateScopes = false;
#endif
        var serviceProvider = services.BuildServiceProvider(validateScopes);

        return new MeterProviderSdk(serviceProvider, ownsServiceProvider: true);
    }

    private MeterProviderBuilder ConfigureBuilderInternal(Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("Builder cannot be configured during MeterProvider construction.");
        }

        services.ConfigureOpenTelemetryMeterProvider(configure);

        return this;
    }

    private MeterProviderBuilder ConfigureServicesInternal(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("Services cannot be configured during MeterProvider construction.");
        }

        configure(services);

        return this;
    }
}
