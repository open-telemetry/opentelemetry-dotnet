// <copyright file="DeferredMeterProviderBuilder.cs" company="OpenTelemetry Authors">
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

using System;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// A <see cref="MeterProviderBuilder"/> implementation which registers build
/// actions as <see cref="IConfigureMeterProviderBuilder"/> instances into an
/// <see cref="IServiceCollection"/>. The build actions should be retrieved
/// using the final application <see cref="IServiceProvider"/> and applied to
/// construct a <see cref="MeterProvider"/>.
/// </summary>
public class DeferredMeterProviderBuilder : MeterProviderBuilder, IMeterProviderBuilder
{
    private IServiceCollection? services;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeferredMeterProviderBuilder"/> class.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    public DeferredMeterProviderBuilder(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        this.services = services;

        RegisterBuildAction(services, (sp, builder) => this.services = null);
    }

    /// <inheritdoc />
    MeterProvider? IMeterProviderBuilder.Provider => null;

    /// <inheritdoc />
    public override MeterProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        Guard.ThrowIfNull(instrumentationFactory);

        this.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(instrumentationFactory);
        });

        return this;
    }

    /// <inheritdoc />
    public override MeterProviderBuilder AddMeter(params string[] names)
    {
        Guard.ThrowIfNull(names);

        this.ConfigureBuilder((sp, builder) =>
        {
            builder.AddMeter(names);
        });

        return this;
    }

    /// <inheritdoc />
    public MeterProviderBuilder ConfigureBuilder(Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("Builder cannot be configured during MeterProvider construction.");
        }

        RegisterBuildAction(services, configure);

        return this;
    }

    /// <inheritdoc />
    public MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("Services cannot be configured during MeterProvider construction.");
        }

        configure((IServiceCollection)services);

        return this;
    }

    /// <inheritdoc />
    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

    private static void RegisterBuildAction(IServiceCollection services, Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        services.AddSingleton<IConfigureMeterProviderBuilder>(
            new ConfigureMeterProviderBuilderCallbackWrapper(configure));
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
