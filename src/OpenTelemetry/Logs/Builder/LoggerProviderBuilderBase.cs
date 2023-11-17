// <copyright file="LoggerProviderBuilderBase.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains methods for building <see cref="LoggerProvider"/> instances.
/// </summary>
internal sealed class LoggerProviderBuilderBase : LoggerProviderBuilder, ILoggerProviderBuilder
{
    private readonly bool allowBuild;
    private readonly LoggerProviderServiceCollectionBuilder innerBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerProviderBuilderBase"/> class.
    /// </summary>
    public LoggerProviderBuilderBase()
    {
        var services = new ServiceCollection();

        services
            .AddOpenTelemetrySharedProviderBuilderServices()
            .AddOpenTelemetryLoggerProviderBuilderServices()
            .TryAddSingleton<LoggerProvider>(
                sp => throw new NotSupportedException("Self-contained LoggerProvider cannot be accessed using the application IServiceProvider call Build instead."));

        this.innerBuilder = new LoggerProviderServiceCollectionBuilder(services);

        this.allowBuild = true;
    }

    internal LoggerProviderBuilderBase(IServiceCollection services, bool addSharedServices)
    {
        Guard.ThrowIfNull(services);

        if (addSharedServices)
        {
            /* Note: This ensures IConfiguration is available when using
             * IServiceCollections NOT attached to a host. For example when
             * performing:
             *
             * new ServiceCollection().AddLogging(b => b.AddOpenTelemetry())
            */
            services.AddOpenTelemetrySharedProviderBuilderServices();
        }

        services
            .AddOpenTelemetryLoggerProviderBuilderServices()
            .TryAddSingleton<LoggerProvider>(sp => new LoggerProviderSdk(sp, ownsServiceProvider: false));

        this.innerBuilder = new LoggerProviderServiceCollectionBuilder(services);

        this.allowBuild = false;
    }

    /// <inheritdoc />
    LoggerProvider? ILoggerProviderBuilder.Provider => null;

    /// <inheritdoc />
    public override LoggerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        this.innerBuilder.AddInstrumentation(instrumentationFactory);

        return this;
    }

    /// <inheritdoc />
    LoggerProviderBuilder ILoggerProviderBuilder.ConfigureServices(Action<IServiceCollection> configure)
    {
        this.innerBuilder.ConfigureServices(configure);

        return this;
    }

    /// <inheritdoc />
    LoggerProviderBuilder IDeferredLoggerProviderBuilder.Configure(Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        this.innerBuilder.ConfigureBuilder(configure);

        return this;
    }

    internal LoggerProvider Build()
    {
        if (!this.allowBuild)
        {
            throw new NotSupportedException("A LoggerProviderBuilder bound to external service cannot be built directly. Access the LoggerProvider using the application IServiceProvider instead.");
        }

        var services = this.innerBuilder.Services
            ?? throw new NotSupportedException("LoggerProviderBuilder build method cannot be called multiple times.");

        this.innerBuilder.Services = null;

#if DEBUG
        bool validateScopes = true;
#else
        bool validateScopes = false;
#endif
        var serviceProvider = services.BuildServiceProvider(validateScopes);

        return new LoggerProviderSdk(serviceProvider, ownsServiceProvider: true);
    }
}
