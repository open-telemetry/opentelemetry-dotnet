// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics;

/// <summary>
/// Contains extension methods for the <see cref="MeterProviderBuilder"/> class.
/// </summary>
public static class OpenTelemetryDependencyInjectionMeterProviderBuilderExtensions
{
    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <remarks>
    /// Note: The type specified by <typeparamref name="T"/> will be
    /// registered as a singleton service into application services.
    /// </remarks>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddInstrumentation<
#if NET
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
    T>(this MeterProviderBuilder meterProviderBuilder)
        where T : class
    {
        meterProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(sp.GetRequiredService<T>);
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="instrumentation">Instrumentation instance.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddInstrumentation<T>(this MeterProviderBuilder meterProviderBuilder, T instrumentation)
        where T : class
    {
        Guard.ThrowIfNull(instrumentation);

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => instrumentation);
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="instrumentationFactory">Instrumentation factory.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddInstrumentation<T>(
        this MeterProviderBuilder meterProviderBuilder,
        Func<IServiceProvider, T> instrumentationFactory)
        where T : class
    {
        Guard.ThrowIfNull(instrumentationFactory);

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => instrumentationFactory(sp));
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="instrumentationFactory">Instrumentation factory.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder AddInstrumentation<T>(
        this MeterProviderBuilder meterProviderBuilder,
        Func<IServiceProvider, MeterProvider, T> instrumentationFactory)
        where T : class
    {
        Guard.ThrowIfNull(instrumentationFactory);

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is IMeterProviderBuilder iMeterProviderBuilder
                && iMeterProviderBuilder.Provider != null)
            {
                builder.AddInstrumentation(() => instrumentationFactory(sp, iMeterProviderBuilder.Provider));
            }
        });

        return meterProviderBuilder;
    }

    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="IServiceCollection"/> where metrics services are configured.
    /// </summary>
    /// <remarks>
    /// Note: Metrics services are only available during the application
    /// configuration phase.
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder ConfigureServices(
        this MeterProviderBuilder meterProviderBuilder,
        Action<IServiceCollection> configure)
    {
        if (meterProviderBuilder is IMeterProviderBuilder iMeterProviderBuilder)
        {
            iMeterProviderBuilder.ConfigureServices(configure);
        }

        return meterProviderBuilder;
    }

    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="MeterProviderBuilder"/> once the application <see
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
    /// href="https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/docs/metrics/customizing-the-sdk/README.md#dependency-injection-support">Dependency
    /// injection support</see>.
    /// </remarks>
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    internal static MeterProviderBuilder ConfigureBuilder(
        this MeterProviderBuilder meterProviderBuilder,
        Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        if (meterProviderBuilder is IDeferredMeterProviderBuilder deferredMeterProviderBuilder)
        {
            deferredMeterProviderBuilder.Configure(configure);
        }

        return meterProviderBuilder;
    }
}
