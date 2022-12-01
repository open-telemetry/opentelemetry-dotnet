// <copyright file="DeferredTracerProviderBuilder.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// A <see cref="TracerProviderBuilder"/> implementation which registers build
/// actions as <see cref="IConfigureTracerProviderBuilder"/> instances into an
/// <see cref="IServiceCollection"/>. The build actions should be retrieved
/// using the final application <see cref="IServiceProvider"/> and applied to
/// construct a <see cref="TracerProvider"/>.
/// </summary>
public class DeferredTracerProviderBuilder : TracerProviderBuilder, ITracerProviderBuilder
{
    private IServiceCollection? services;

    /// <summary>
    /// Initializes a new instance of the <see cref="DeferredTracerProviderBuilder"/> class.
    /// </summary>
    /// <param name="services"><see cref="IServiceCollection"/>.</param>
    public DeferredTracerProviderBuilder(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        this.services = services;

        RegisterBuildAction(services, (sp, builder) => this.services = null);
    }

    /// <inheritdoc />
    TracerProvider? ITracerProviderBuilder.Provider => null;

    /// <inheritdoc />
    public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        Guard.ThrowIfNull(instrumentationFactory);

        this.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(instrumentationFactory);
        });

        return this;
    }

    /// <inheritdoc />
    public override TracerProviderBuilder AddSource(params string[] names)
    {
        Guard.ThrowIfNull(names);

        this.ConfigureBuilder((sp, builder) =>
        {
            builder.AddSource(names);
        });

        return this;
    }

    /// <inheritdoc />
    public override TracerProviderBuilder AddLegacySource(string operationName)
    {
        Guard.ThrowIfNullOrWhitespace(operationName);

        this.ConfigureBuilder((sp, builder) =>
        {
            builder.AddLegacySource(operationName);
        });

        return this;
    }

    /// <inheritdoc />
    public TracerProviderBuilder ConfigureBuilder(Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("Builder cannot be configured during TracerProvider construction.");
        }

        RegisterBuildAction(services, configure);

        return this;
    }

    /// <inheritdoc />
    public TracerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("Services cannot be configured during TracerProvider construction.");
        }

        configure(services);

        return this;
    }

    /// <inheritdoc />
    TracerProviderBuilder IDeferredTracerProviderBuilder.Configure(Action<IServiceProvider, TracerProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

    private static void RegisterBuildAction(IServiceCollection services, Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        services.AddSingleton<IConfigureTracerProviderBuilder>(
            new ConfigureTracerProviderBuilderCallbackWrapper(configure));
    }

    private sealed class ConfigureTracerProviderBuilderCallbackWrapper : IConfigureTracerProviderBuilder
    {
        private readonly Action<IServiceProvider, TracerProviderBuilder> configure;

        public ConfigureTracerProviderBuilderCallbackWrapper(Action<IServiceProvider, TracerProviderBuilder> configure)
        {
            Guard.ThrowIfNull(configure);

            this.configure = configure;
        }

        public void ConfigureBuilder(IServiceProvider serviceProvider, TracerProviderBuilder tracerProviderBuilder)
        {
            this.configure(serviceProvider, tracerProviderBuilder);
        }
    }
}
