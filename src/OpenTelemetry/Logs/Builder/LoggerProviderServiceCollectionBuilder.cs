// <copyright file="LoggerProviderServiceCollectionBuilder.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains methods for registering actions into an <see
/// cref="IServiceCollection"/> which will be used to build a <see
/// cref="LoggerProvider"/> once the <see cref="IServiceProvider"/> is
/// available.
/// </summary>
internal sealed class LoggerProviderServiceCollectionBuilder : LoggerProviderBuilder, ILoggerProviderBuilder
{
    private readonly bool allowBuild;
    private IServiceCollection? services;

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerProviderServiceCollectionBuilder"/> class.
    /// </summary>
    public LoggerProviderServiceCollectionBuilder()
    {
        var services = new ServiceCollection();

        services
            .AddOpenTelemetrySharedProviderBuilderServices()
            .AddOpenTelemetryLoggerProviderBuilderServices()
            .TryAddSingleton<LoggerProvider>(
                sp => throw new NotSupportedException("Self-contained LoggerProvider cannot be accessed using the application IServiceProvider call Build instead."));

        services.ConfigureOpenTelemetryLoggerProvider((sp, builder) => this.services = null);

        this.services = services;

        this.allowBuild = true;
    }

    internal LoggerProviderServiceCollectionBuilder(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services
            .AddOpenTelemetryLoggerProviderBuilderServices()
            .TryAddSingleton<LoggerProvider>(sp => new LoggerProviderSdk(sp, ownsServiceProvider: false));

        services.ConfigureOpenTelemetryLoggerProvider((sp, builder) => this.services = null);

        this.services = services;

        this.allowBuild = false;
    }

    /// <inheritdoc />
    LoggerProvider? ILoggerProviderBuilder.Provider => null;

    /// <inheritdoc />
    public override LoggerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        Guard.ThrowIfNull(instrumentationFactory);

        this.ConfigureBuilderInternal((sp, builder) =>
        {
            builder.AddInstrumentation(instrumentationFactory);
        });

        return this;
    }

    /// <inheritdoc />
    LoggerProviderBuilder ILoggerProviderBuilder.ConfigureServices(Action<IServiceCollection> configure)
        => this.ConfigureServicesInternal(configure);

    /// <inheritdoc />
    LoggerProviderBuilder IDeferredLoggerProviderBuilder.Configure(Action<IServiceProvider, LoggerProviderBuilder> configure)
        => this.ConfigureBuilderInternal(configure);

    internal LoggerProvider Build()
    {
        if (!this.allowBuild)
        {
            throw new NotSupportedException("A LoggerProviderBuilder bound to external service cannot be built directly. Access the LoggerProvider using the application IServiceProvider instead.");
        }

        var services = this.services
            ?? throw new NotSupportedException("LoggerProviderBuilder build method cannot be called multiple times.");

        this.services = null;

#if DEBUG
        bool validateScopes = true;
#else
        bool validateScopes = false;
#endif
        var serviceProvider = services.BuildServiceProvider(validateScopes);

        return new LoggerProviderSdk(serviceProvider, ownsServiceProvider: true);
    }

    private LoggerProviderBuilder ConfigureBuilderInternal(Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        var services = this.services
            ?? throw new NotSupportedException("Builder cannot be configured during LoggerProvider construction.");

        services.ConfigureOpenTelemetryLoggerProvider(configure);

        return this;
    }

    private LoggerProviderBuilder ConfigureServicesInternal(Action<IServiceCollection> configure)
    {
        Guard.ThrowIfNull(configure);

        var services = this.services
            ?? throw new NotSupportedException("Services cannot be configured during LoggerProvider construction.");

        configure(services);

        return this;
    }
}
