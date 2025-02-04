// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace;

/// <summary>
/// Contains extension methods for the <see cref="TracerProviderBuilder"/> class.
/// </summary>
public static class OpenTelemetryDependencyInjectionTracerProviderBuilderExtensions
{
    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <remarks>
    /// Note: The type specified by <typeparamref name="T"/> will be
    /// registered as a singleton service into application services.
    /// </remarks>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddInstrumentation<
#if NET
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
    T>(this TracerProviderBuilder tracerProviderBuilder)
        where T : class
    {
        tracerProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(sp.GetRequiredService<T>);
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="instrumentation">Instrumentation instance.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddInstrumentation<T>(this TracerProviderBuilder tracerProviderBuilder, T instrumentation)
        where T : class
    {
        Guard.ThrowIfNull(instrumentation);

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => instrumentation);
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="instrumentationFactory">Instrumentation factory.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddInstrumentation<T>(
        this TracerProviderBuilder tracerProviderBuilder,
        Func<IServiceProvider, T> instrumentationFactory)
        where T : class
    {
        Guard.ThrowIfNull(instrumentationFactory);

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => instrumentationFactory(sp));
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="instrumentationFactory">Instrumentation factory.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder AddInstrumentation<T>(
        this TracerProviderBuilder tracerProviderBuilder,
        Func<IServiceProvider, TracerProvider, T> instrumentationFactory)
        where T : class
    {
        Guard.ThrowIfNull(instrumentationFactory);

        tracerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is ITracerProviderBuilder iTracerProviderBuilder
                && iTracerProviderBuilder.Provider != null)
            {
                builder.AddInstrumentation(() => instrumentationFactory(sp, iTracerProviderBuilder.Provider));
            }
        });

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="IServiceCollection"/> where tracing services are configured.
    /// </summary>
    /// <remarks>
    /// Note: Tracing services are only available during the application
    /// configuration phase.
    /// </remarks>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    public static TracerProviderBuilder ConfigureServices(
        this TracerProviderBuilder tracerProviderBuilder,
        Action<IServiceCollection> configure)
    {
        if (tracerProviderBuilder is ITracerProviderBuilder iTracerProviderBuilder)
        {
            iTracerProviderBuilder.ConfigureServices(configure);
        }

        return tracerProviderBuilder;
    }

    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="TracerProviderBuilder"/> once the application <see
    /// cref="IServiceProvider"/> is available.
    /// </summary>
    /// <remarks>
    /// <para><see cref="ConfigureBuilder"/> is an advanced API and is expected
    /// to be used primarily by library authors.</para>
    /// Notes:
    /// <list type="bullet">
    /// <item>Services may NOT be added to the <see cref="IServiceCollection" />
    /// (via <see cref="ConfigureServices"/>) inside <see
    /// cref="ConfigureBuilder"/> because the <see cref="IServiceProvider"/> has
    /// already been created. A <see cref="NotSupportedException"/> will be
    /// thrown if services are accessed.</item>
    /// <item>Library extension methods (for example <c>AddOtlpExporter</c>
    /// inside <c>OpenTelemetry.Exporter.OpenTelemetryProtocol</c>) may depend
    /// on services being available today or at any point in the future. It is
    /// NOT recommend to call library extension methods from inside <see
    /// cref="ConfigureBuilder"/>.</item>
    /// </list>
    /// For more information see: <see
    /// href="https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/trace/customizing-the-sdk/README.md#dependency-injection-support">Dependency
    /// injection support</see>.
    /// </remarks>
    /// <param name="tracerProviderBuilder"><see cref="TracerProviderBuilder"/>.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="TracerProviderBuilder"/> for chaining.</returns>
    internal static TracerProviderBuilder ConfigureBuilder(
        this TracerProviderBuilder tracerProviderBuilder,
        Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        if (tracerProviderBuilder is IDeferredTracerProviderBuilder deferredTracerProviderBuilder)
        {
            deferredTracerProviderBuilder.Configure(configure);
        }

        return tracerProviderBuilder;
    }
}
