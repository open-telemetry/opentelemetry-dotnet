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
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.DependencyInjection;

public class DeferredTracerProviderBuilder : TracerProviderBuilder, ITracerProviderBuilder
{
    public DeferredTracerProviderBuilder(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        this.Services = services;

        this.ConfigureBuilder((sp, builder) => this.Services = null);
    }

    /// <inheritdoc />
    TracerProvider? ITracerProviderBuilder.Provider => null;

    protected IServiceCollection? Services { get; set; }

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
        this.ConfigureServices(services => services.AddSingleton<IConfigureTracerProviderBuilder>(
            new ConfigureTracerProviderBuilderCallbackWrapper(configure)));

        return this;
    }

    /// <inheritdoc />
    public TracerProviderBuilder ConfigureServices(Action<IServiceCollection> configure)
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
    TracerProviderBuilder IDeferredTracerProviderBuilder.Configure(Action<IServiceProvider, TracerProviderBuilder> configure)
        => this.ConfigureBuilder(configure);

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
