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

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Contains methods for building <see cref="TracerProvider"/> instances.
/// </summary>
public class TracerProviderBuilderBase : DeferredTracerProviderBuilder
{
    public TracerProviderBuilderBase()
        : base(new ServiceCollection())
    {
        this.ConfigureServices(services => services
            .AddOpenTelemetryTracerProviderBuilderServices()
            .TryAddSingleton<TracerProvider>(
                sp => throw new NotSupportedException("Self-contained TracerProvider cannot be accessed using the application IServiceProvider call Build instead.")));
    }

    internal static void RegisterTracerProvider(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services
            .AddOpenTelemetryTracerProviderBuilderServices()
            .TryAddSingleton<TracerProvider>(sp => new TracerProviderSdk(sp, ownsServiceProvider: false));
    }

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

        return this.ConfigureBuilder((sp, builder) =>
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
        ServiceProvider? serviceProvider = null;
        try
        {
            this.ConfigureServices(services =>
            {
#if DEBUG
                bool validateScopes = true;
#else
                bool validateScopes = false;
#endif
                serviceProvider = services.BuildServiceProvider(validateScopes);
            });
        }
        catch (NotSupportedException)
        {
            throw new NotSupportedException("TracerProviderBuilder build method cannot be called multiple times.");
        }

        if (serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceProvider could not be created for ServiceCollection.");
        }

        return new TracerProviderSdk(serviceProvider, ownsServiceProvider: true);
    }
}
