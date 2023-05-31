// <copyright file="MeterProviderServiceCollectionBuilder.cs" company="OpenTelemetry Authors">
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

    private MeterProviderBuilder ConfigureBuilderInternal(Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        var services = this.Services
            ?? throw new NotSupportedException("Builder cannot be configured during MeterProvider construction.");

        services.ConfigureOpenTelemetryMeterProvider(configure);

        return this;
    }

    private MeterProviderBuilder ConfigureServicesInternal(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.Services
            ?? throw new NotSupportedException("Services cannot be configured during MeterProvider construction.");

        configure(services);

        return this;
    }
}
