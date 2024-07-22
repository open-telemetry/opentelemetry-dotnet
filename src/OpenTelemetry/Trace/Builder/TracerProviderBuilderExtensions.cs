// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Trace;

/// <summary>
/// Contains extension methods for the <see cref="TracerProviderBuilder"/> class.
/// </summary>
public static class TracerProviderBuilderExtensions
{
    /// <summary>
    /// Sets whether the status of <see cref="Activity"/>
    /// should be set to <c>Status.Error</c> when it ended abnormally due to an unhandled exception.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="enabled">Enabled or not. Default value is <c>true</c>.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    /// <remarks>
    /// This method is not supported in native AOT or Mono Runtime as of .NET 8.
    /// </remarks>
#if NET7_0_OR_GREATER
    [RequiresDynamicCode("The code for detecting exception and setting error status might not be available.")]
#endif
    public static TracerProviderBuilder SetErrorStatusOnException(this TracerProviderBuilder tracerProviderBuilder, bool enabled = true)
    {
        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.SetErrorStatusOnException(enabled);
            }
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Sets sampler.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="sampler">Sampler instance.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder SetSampler(this TracerProviderBuilder tracerProviderBuilder, Sampler sampler)
    {
        Guard.ThrowIfNull(sampler);

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.SetSampler(sampler);
            }
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Sets the sampler on the provider.
    /// </summary>
    /// <remarks>
    /// Note: The type specified by <typeparamref name="T"/> will be
    /// registered as a singleton service into application services.
    /// </remarks>
    /// <typeparam name="T">Sampler type.</typeparam>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder SetSampler<
#if NET
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
    T>(this TracerProviderBuilder tracerProviderBuilder)
        where T : Sampler
    {
        tracerProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.SetSampler(sp.GetRequiredService<T>());
            }
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Sets the sampler on the provider.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder SetSampler(
        this TracerProviderBuilder tracerProviderBuilder,
        Func<IServiceProvider, Sampler> implementationFactory)
    {
        Guard.ThrowIfNull(implementationFactory);

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.SetSampler(implementationFactory(sp));
            }
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Sets the <see cref="ResourceBuilder"/> from which the Resource associated with
    /// this provider is built from. Overwrites currently set ResourceBuilder.
    /// You should usually use <see cref="ConfigureResource(TracerProviderBuilder, Action{ResourceBuilder})"/> instead
    /// (call <see cref="ResourceBuilder.Clear"/> if desired).
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="resourceBuilder"><see cref="ResourceBuilder"/> from which Resource will be built.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder SetResourceBuilder(this TracerProviderBuilder tracerProviderBuilder, ResourceBuilder resourceBuilder)
    {
        Guard.ThrowIfNull(resourceBuilder);

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.SetResourceBuilder(resourceBuilder);
            }
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Modify the <see cref="ResourceBuilder"/> from which the Resource associated with
    /// this provider is built from in-place.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="configure">An action which modifies the provided <see cref="ResourceBuilder"/> in-place.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder ConfigureResource(this TracerProviderBuilder tracerProviderBuilder, Action<ResourceBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.ConfigureResource(configure);
            }
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Adds a processor to the provider.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="processor">Activity processor to add.</param>
    /// <returns>Returns <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddProcessor(this TracerProviderBuilder tracerProviderBuilder, BaseProcessor<Activity> processor)
    {
        Guard.ThrowIfNull(processor);

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.AddProcessor(processor);
            }
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Adds a processor to the provider which will be retrieved using dependency injection.
    /// </summary>
    /// <remarks>
    /// Note: The type specified by <typeparamref name="T"/> will be
    /// registered as a singleton service into application services.
    /// </remarks>
    /// <typeparam name="T">Processor type.</typeparam>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddProcessor<
#if NET
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
    T>(this TracerProviderBuilder tracerProviderBuilder)
        where T : BaseProcessor<Activity>
    {
        tracerProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.AddProcessor(sp.GetRequiredService<T>());
            }
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Adds a processor to the provider which will be retrieved using dependency injection.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="implementationFactory">The factory that creates the service.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddProcessor(
        this TracerProviderBuilder tracerProviderBuilder,
        Func<IServiceProvider, BaseProcessor<Activity>> implementationFactory)
    {
        Guard.ThrowIfNull(implementationFactory);

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is TracerProviderBuilderSdk tracerProviderBuilderSdk)
            {
                tracerProviderBuilderSdk.AddProcessor(implementationFactory(sp));
            }
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Adds a <see cref="BatchActivityExportProcessor"/> to the provider for the supplied exporter.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="exporter"><see cref="BaseExporter{T}"/>.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddBatchExportProcessor(
        this TracerProviderBuilder tracerProviderBuilder,
        BaseExporter<Activity> exporter)
        => AddBatchExportProcessor(tracerProviderBuilder, name: null, exporter);

    /// <summary>
    /// Adds a <see cref="BatchActivityExportProcessor"/> to the provider for the supplied exporter.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="exporter"><see cref="BaseExporter{T}"/>.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddBatchExportProcessor(
        this TracerProviderBuilder tracerProviderBuilder,
        string? name,
        BaseExporter<Activity> exporter)
    {
        Guard.ThrowIfNull(exporter);

        return AddBatchExportProcessor(
            tracerProviderBuilder,
            name,
            implementationFactory: (sp, name) => exporter);
    }

    /// <summary>
    /// Adds a <see cref="BatchActivityExportProcessor"/> to the provider for the supplied exporter.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="implementationFactory">Factory function used to create the exporter.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddBatchExportProcessor(
        this TracerProviderBuilder tracerProviderBuilder,
        Func<IServiceProvider, BaseExporter<Activity>> implementationFactory)
    {
        Guard.ThrowIfNull(implementationFactory);

        return AddBatchExportProcessor(
            tracerProviderBuilder,
            name: null,
            implementationFactory: (sp, name) => implementationFactory(sp));
    }

    /// <summary>
    /// Adds a <see cref="BatchActivityExportProcessor"/> to the provider for the supplied exporter.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="implementationFactory">Factory function used to create the exporter.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddBatchExportProcessor(
        this TracerProviderBuilder tracerProviderBuilder,
        string? name,
        Func<IServiceProvider, string?, BaseExporter<Activity>> implementationFactory)
    {
        Guard.ThrowIfNull(implementationFactory);

        return tracerProviderBuilder.AddProcessor(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<ActivityExportProcessorOptions>>().Get(name);

            var exporter = implementationFactory(sp, name)
                ?? throw new InvalidOperationException("Implementation factory returned a null instance");

            return ActivityExportProcessorFactory.CreateBatchExportProcessor(options, exporter);
        });
    }

    /// <summary>
    /// Adds a <see cref="SimpleActivityExportProcessor"/> to the provider for the supplied exporter.
    /// </summary>
    /// <remarks>
    /// Note: The concurrency behavior of the constructed <see
    /// cref="SimpleActivityExportProcessor"/> can be controlled by decorating
    /// the exporter with the <see cref="ConcurrencyModesAttribute"/>.
    /// </remarks>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="exporter"><see cref="BaseExporter{T}"/>.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddSimpleExportProcessor(
        this TracerProviderBuilder tracerProviderBuilder,
        BaseExporter<Activity> exporter)
        => AddSimpleExportProcessor(tracerProviderBuilder, name: null, exporter);

    /// <summary>
    /// Adds a <see cref="SimpleActivityExportProcessor"/> to the provider for the supplied exporter.
    /// </summary>
    /// <remarks><inheritdoc cref="AddSimpleExportProcessor(TracerProviderBuilder, BaseExporter{Activity})" path="/remarks"/></remarks>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="exporter"><see cref="BaseExporter{T}"/>.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddSimpleExportProcessor(
        this TracerProviderBuilder tracerProviderBuilder,
        string? name,
        BaseExporter<Activity> exporter)
    {
        Guard.ThrowIfNull(exporter);

        return AddSimpleExportProcessor(
            tracerProviderBuilder,
            name,
            implementationFactory: (sp, name) => exporter);
    }

    /// <summary>
    /// Adds a <see cref="SimpleActivityExportProcessor"/> to the provider for the supplied exporter.
    /// </summary>
    /// <remarks><inheritdoc cref="AddSimpleExportProcessor(TracerProviderBuilder, BaseExporter{Activity})" path="/remarks"/></remarks>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="implementationFactory">Factory function used to create the exporter.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddSimpleExportProcessor(
        this TracerProviderBuilder tracerProviderBuilder,
        Func<IServiceProvider, BaseExporter<Activity>> implementationFactory)
    {
        Guard.ThrowIfNull(implementationFactory);

        return AddSimpleExportProcessor(
            tracerProviderBuilder,
            name: null,
            implementationFactory: (sp, name) => implementationFactory(sp));
    }

    /// <summary>
    /// Adds a <see cref="SimpleActivityExportProcessor"/> to the provider for the supplied exporter.
    /// </summary>
    /// <remarks><inheritdoc cref="AddSimpleExportProcessor(TracerProviderBuilder, BaseExporter{Activity})" path="/remarks"/></remarks>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="name">Optional name which is used when retrieving options.</param>
    /// <param name="implementationFactory">Factory function used to create the exporter.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddSimpleExportProcessor(
        this TracerProviderBuilder tracerProviderBuilder,
        string? name,
        Func<IServiceProvider, string?, BaseExporter<Activity>> implementationFactory)
    {
        Guard.ThrowIfNull(implementationFactory);

        return tracerProviderBuilder.AddProcessor(sp =>
        {
            var options = sp.GetRequiredService<IOptionsMonitor<ActivityExportProcessorOptions>>().Get(name);

            var exporter = implementationFactory(sp, name)
                ?? throw new InvalidOperationException("Implementation factory returned a null instance");

            return ActivityExportProcessorFactory.CreateSimpleExportProcessor(options, exporter);
        });
    }

    /// <summary>
    /// Run the given actions to initialize the <see cref="TracerProvider"/>.
    /// </summary>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <returns><see cref="TracerProvider"/>.</returns>
    public static TracerProvider Build(this TracerProviderBuilder tracerProviderBuilder)
    {
        if (tracerProviderBuilder is TracerProviderBuilderBase tracerProviderBuilderBase)
        {
            return tracerProviderBuilderBase.InvokeBuild();
        }

        throw new NotSupportedException($"Build is not supported on '{tracerProviderBuilder?.GetType().FullName ?? "null"}' instances.");
    }
}
