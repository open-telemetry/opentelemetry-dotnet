// <copyright file="TracerProviderBuilderBase.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace;

/// <summary>
/// Contains methods for building <see cref="TracerProvider"/> instances.
/// </summary>
public class TracerProviderBuilderBase : TracerProviderBuilder, ITracerProviderBuilder
{
    private readonly bool allowBuild;
    private IServiceCollection? services;

    /// <summary>
    /// Initializes a new instance of the <see cref="TracerProviderBuilderBase"/> class.
    /// </summary>
    public TracerProviderBuilderBase()
    {
        var services = new ServiceCollection();

        services
            .AddOpenTelemetrySharedProviderBuilderServices()
            .AddOpenTelemetryTracerProviderBuilderServices()
            .TryAddSingleton<TracerProvider>(
                sp => throw new NotSupportedException("Self-contained TracerProvider cannot be accessed using the application IServiceProvider call Build instead."));

        services.ConfigureOpenTelemetryTracerProvider((sp, builder) => this.services = null);

        this.services = services;

        this.allowBuild = true;
    }

    internal TracerProviderBuilderBase(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services
            .AddOpenTelemetryTracerProviderBuilderServices()
            .TryAddSingleton<TracerProvider>(sp => new TracerProviderSdk(sp, ownsServiceProvider: false));

        services.ConfigureOpenTelemetryTracerProvider((sp, builder) => this.services = null);

        this.services = services;

        this.allowBuild = false;
    }

    /// <inheritdoc />
    TracerProvider? ITracerProviderBuilder.Provider => null;

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
    TracerProviderBuilder ITracerProviderBuilder.ConfigureServices(Action<IServiceCollection> configure)
        => this.ConfigureServicesInternal(configure);

    /// <inheritdoc />
    TracerProviderBuilder IDeferredTracerProviderBuilder.Configure(Action<IServiceProvider, TracerProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

    internal TracerProvider InvokeBuild()
        => this.Build();

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>d
    /// <param name="instrumentationName">Instrumentation name.</param>
    /// <param name="instrumentationVersion">Instrumentation version.</param>
    /// <param name="instrumentationFactory">Function that builds instrumentation.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    protected TracerProviderBuilder AddInstrumentation(
        string instrumentationName,
        string instrumentationVersion,
        Func<object> instrumentationFactory)
    {
        Guard.ThrowIfNullOrWhitespace(instrumentationName);
        Guard.ThrowIfNullOrWhitespace(instrumentationVersion);
        Guard.ThrowIfNull(instrumentationFactory);

        return this.ConfigureBuilderInternal((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderState)
            {
                tracerProviderBuilderState.AddInstrumentation(
                    instrumentationName,
                    instrumentationVersion,
                    instrumentationFactory);
            }
        });
    }

    /// <summary>
    /// Run the configured actions to initialize the <see cref="TracerProvider"/>.
    /// </summary>
    /// <returns><see cref="TracerProvider"/>.</returns>
    protected TracerProvider Build()
    {
        if (!this.allowBuild)
        {
            throw new NotSupportedException("A TracerProviderBuilder bound to external service cannot be built directly. Access the TracerProvider using the application IServiceProvider instead.");
        }

        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("TracerProviderBuilder build method cannot be called multiple times.");
        }

        this.services = null;

#if DEBUG
        bool validateScopes = true;
#else
        bool validateScopes = false;
#endif
        var serviceProvider = services.BuildServiceProvider(validateScopes);

        return new TracerProviderSdk(serviceProvider, ownsServiceProvider: true);
    }

    private TracerProviderBuilder ConfigureBuilderInternal(Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        var services = this.services;

        if (services == null)
        {
            throw new NotSupportedException("Builder cannot be configured during TracerProvider construction.");
        }

        services.ConfigureOpenTelemetryTracerProvider(configure);

        return this;
    }

    private TracerProviderBuilder ConfigureServicesInternal(Action<IServiceCollection> configure)
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
}
