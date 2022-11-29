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
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using OpenTelemetry.Metrics;

namespace Microsoft.Extensions.DependencyInjection;

public class DeferredMeterProviderBuilder : MeterProviderBuilder, IMeterProviderBuilder
{
    public DeferredMeterProviderBuilder(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        this.Services = services;

        services.TryAddSingleton<MeterProvider>(sp => throw new NotSupportedException("The OpenTelemetry SDK has not been initialized. Call AddOpenTelemetry to register the SDK."));

        this.ConfigureBuilder((sp, builder) => this.Services = null);
    }

    /// <inheritdoc />
    MeterProvider? IMeterProviderBuilder.Provider => null;

    protected IServiceCollection? Services { get; set; }

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
        this.ConfigureServices(services => services.AddSingleton<IConfigureMeterProviderBuilder>(
            new ConfigureMeterProviderBuilderCallbackWrapper(configure)));

        return this;
    }

    /// <inheritdoc />
    public MeterProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.Services;

        if (services == null)
        {
            throw new NotSupportedException("Services cannot be configured after ServiceProvider has been created.");
        }

        configure(services);

        return this;
    }

    /// <inheritdoc />
    MeterProviderBuilder IDeferredMeterProviderBuilder.Configure(Action<IServiceProvider, MeterProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

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
