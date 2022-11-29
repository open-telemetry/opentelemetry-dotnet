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

    internal MeterProvider InvokeBuild()
        => this.Build();

    /// <summary>
    /// Run the configured actions to initialize the <see cref="MeterProvider"/>.
    /// </summary>
    /// <returns><see cref="MeterProvider"/>.</returns>
    protected MeterProvider Build()
    {
        var services = this.Services;

        if (services == null)
        {
            throw new NotSupportedException("MeterProviderBuilder build method cannot be called multiple times.");
        }

        this.Services = null;

#if DEBUG
        bool validateScopes = true;
#else
        bool validateScopes = false;
#endif
        var serviceProvider = services.BuildServiceProvider(validateScopes);

        return new MeterProviderSdk(serviceProvider, ownsServiceProvider: true);
    }
}
