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
            .AddSingleton<TracerProvider>(
                sp => throw new NotSupportedException("External TracerProvider created through Sdk.CreateTracerProviderBuilder cannot be accessed using service provider.")));
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
        var services = this.Services;

        if (services == null)
        {
            throw new NotSupportedException("TracerProviderBuilder build method cannot be called multiple times.");
        }

        this.Services = null;

#if DEBUG
        bool validateScopes = true;
#else
        bool validateScopes = false;
#endif
        var serviceProvider = services.BuildServiceProvider(validateScopes);

        return new TracerProviderSdk(serviceProvider, ownsServiceProvider: true);
    }
}
