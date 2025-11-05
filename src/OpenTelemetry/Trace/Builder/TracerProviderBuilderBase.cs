// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;
using static OpenTelemetry.OpenTelemetrySdk;

namespace OpenTelemetry.Trace;

/// <summary>
/// Contains methods for building <see cref="TracerProvider"/> instances.
/// </summary>
public class TracerProviderBuilderBase : TracerProviderBuilder, ITracerProviderBuilder
{
    private readonly bool allowBuild;
    private readonly TracerProviderServiceCollectionBuilder innerBuilder;

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

        this.innerBuilder = new TracerProviderServiceCollectionBuilder(services);

        this.allowBuild = true;
    }

    internal TracerProviderBuilderBase(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services
            .AddOpenTelemetryTracerProviderBuilderServices()
            .TryAddSingleton<TracerProvider>(sp =>
            {
                if (IsOtelSdkDisabled(sp.GetRequiredService<IConfiguration>()))
                {
                    var noopTracerProvider = new NoopTracerProvider();
                    noopTracerProvider.Dispose();
                    return noopTracerProvider;
                }

                return new TracerProviderSdk(sp, ownsServiceProvider: false);
            });

        this.innerBuilder = new TracerProviderServiceCollectionBuilder(services);

        this.allowBuild = false;
    }

    /// <inheritdoc />
    TracerProvider? ITracerProviderBuilder.Provider => null;

    /// <inheritdoc />
    public override TracerProviderBuilder AddInstrumentation<TInstrumentation>(Func<TInstrumentation> instrumentationFactory)
    {
        this.innerBuilder.AddInstrumentation(instrumentationFactory);

        return this;
    }

    /// <inheritdoc />
    public override TracerProviderBuilder AddSource(params string[] names)
    {
        this.innerBuilder.AddSource(names);

        return this;
    }

    /// <inheritdoc />
    public override TracerProviderBuilder AddLegacySource(string operationName)
    {
        this.innerBuilder.AddLegacySource(operationName);

        return this;
    }

    /// <inheritdoc />
    TracerProviderBuilder ITracerProviderBuilder.ConfigureServices(Action<IServiceCollection> configure)
    {
        this.innerBuilder.ConfigureServices(configure);

        return this;
    }

    /// <inheritdoc />
#pragma warning disable CA1033 // Interface methods should be callable by child types
    TracerProviderBuilder IDeferredTracerProviderBuilder.Configure(Action<IServiceProvider, TracerProviderBuilder> configure)
#pragma warning restore CA1033 // Interface methods should be callable by child types
    {
        this.innerBuilder.ConfigureBuilder(configure);

        return this;
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
        Func<object?> instrumentationFactory)
    {
        Guard.ThrowIfNullOrWhitespace(instrumentationName);
        Guard.ThrowIfNullOrWhitespace(instrumentationVersion);
        Guard.ThrowIfNull(instrumentationFactory);

        this.innerBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderState)
            {
                tracerProviderBuilderState.AddInstrumentation(
                    instrumentationName,
                    instrumentationVersion,
                    instrumentationFactory());
            }
        });

        return this;
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

        var services = this.innerBuilder.Services
            ?? throw new NotSupportedException("TracerProviderBuilder build method cannot be called multiple times.");

        this.innerBuilder.Services = null;

#if DEBUG
        bool validateScopes = true;
#else
        bool validateScopes = false;
#endif
        var serviceProvider = services.BuildServiceProvider(validateScopes);
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();

        if (IsOtelSdkDisabled(configuration))
        {
            serviceProvider.Dispose();
            return new NoopTracerProvider();
        }

        return new TracerProviderSdk(serviceProvider, ownsServiceProvider: true);
    }

    private static bool IsOtelSdkDisabled(IConfiguration configuration)
    {
        bool isDisabled = configuration.TryGetBoolValue(OpenTelemetrySdkEventSource.Log, SdkConfigDefinitions.SdkDisableEnvVarName, out bool result) && result;
        if (isDisabled)
        {
            OpenTelemetrySdkEventSource.Log.TracerProviderSdkEvent($"Disabled because {SdkConfigDefinitions.SdkDisableEnvVarName} is true.");
        }

        return isDisabled;
    }
}
