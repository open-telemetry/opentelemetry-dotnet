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

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains methods for building <see cref="MeterProvider"/> instances.
/// </summary>
public class MeterProviderBuilderBase : DeferredMeterProviderBuilder
{
    public MeterProviderBuilderBase()
        : base(new ServiceCollection())
    {
        this.ConfigureServices(services => services
            .AddOpenTelemetryMeterProviderBuilderServices()
            .TryAddSingleton<MeterProvider>(
                sp => throw new NotSupportedException("Self-contained MeterProvider cannot be accessed using the application IServiceProvider call Build instead.")));
    }

    internal static void RegisterMeterProvider(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services
            .AddOpenTelemetryMeterProviderBuilderServices()
            .TryAddSingleton<MeterProvider>(sp => new MeterProviderSdk(sp, ownsServiceProvider: false));
    }

    internal MeterProvider InvokeBuild()
        => this.Build();

    /// <summary>
    /// Run the configured actions to initialize the <see cref="MeterProvider"/>.
    /// </summary>
    /// <returns><see cref="MeterProvider"/>.</returns>
    protected MeterProvider Build()
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
            throw new NotSupportedException("MeterProviderBuilder build method cannot be called multiple times.");
        }

        if (serviceProvider == null)
        {
            throw new InvalidOperationException("ServiceProvider could not be created for ServiceCollection.");
        }

        return new MeterProviderSdk(serviceProvider, ownsServiceProvider: true);
    }
}
