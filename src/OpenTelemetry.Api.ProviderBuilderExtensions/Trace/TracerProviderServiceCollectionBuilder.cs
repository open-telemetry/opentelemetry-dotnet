// <copyright file="TracerProviderServiceCollectionBuilder.cs" company="OpenTelemetry Authors">
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

    private TracerProviderBuilder ConfigureBuilderInternal(Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        var services = this.Services
            ?? throw new NotSupportedException("Builder cannot be configured during TracerProvider construction.");

        services.ConfigureOpenTelemetryTracerProvider(configure);

        return this;
    }

    private TracerProviderBuilder ConfigureServicesInternal(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.Services
            ?? throw new NotSupportedException("Services cannot be configured during TracerProvider construction.");

        configure(services);

        return this;
    }
}
