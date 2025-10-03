// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using static OpenTelemetry.OpenTelemetrySdk;

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

    internal LoggerProviderBuilderBase(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

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
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        configuration.TryGetStringValue(SdkConfigDefinitions.SdkDisableEnvVarName, out var envVarValue);

        if (bool.TryParse(envVarValue, out bool result) && result)
        {
            serviceProvider.Dispose();
            return new NoopLoggerProvider();
        }

        return new LoggerProviderSdk(serviceProvider, ownsServiceProvider: true);
    }
}
