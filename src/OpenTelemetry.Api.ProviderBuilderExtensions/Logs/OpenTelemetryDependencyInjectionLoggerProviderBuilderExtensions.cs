// <copyright file="OpenTelemetryDependencyInjectionLoggerProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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

#if NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// Contains extension methods for the <see cref="LoggerProviderBuilder"/> class.
/// </summary>
#if EXPOSE_EXPERIMENTAL_FEATURES
    public
#else
    internal
#endif
    static class OpenTelemetryDependencyInjectionLoggerProviderBuilderExtensions
{
#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <remarks>
    /// <para><inheritdoc cref="AddInstrumentation{T}(LoggerProviderBuilder, T)" path="/remarks"/></para>
    /// Note: The type specified by <typeparamref name="T"/> will be
    /// registered as a singleton service into application services.
    /// </remarks>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
#else
    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <remarks>
    /// Note: The type specified by <typeparamref name="T"/> will be
    /// registered as a singleton service into application services.
    /// </remarks>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
#endif
    public static LoggerProviderBuilder AddInstrumentation<
#if NET6_0_OR_GREATER
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)]
#endif
        T>(this LoggerProviderBuilder loggerProviderBuilder)
        where T : class
    {
        loggerProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

        loggerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => sp.GetRequiredService<T>());
        });

        return loggerProviderBuilder;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <remarks><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</remarks>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="instrumentation">Instrumentation instance.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
#else
    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="instrumentation">Instrumentation instance.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
#endif
    public static LoggerProviderBuilder AddInstrumentation<T>(this LoggerProviderBuilder loggerProviderBuilder, T instrumentation)
        where T : class
    {
        Guard.ThrowIfNull(instrumentation);

        loggerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => instrumentation);
        });

        return loggerProviderBuilder;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <remarks><inheritdoc cref="AddInstrumentation{T}(LoggerProviderBuilder, T)" path="/remarks"/></remarks>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="instrumentationFactory">Instrumentation factory.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
#else
    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="instrumentationFactory">Instrumentation factory.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
#endif
    public static LoggerProviderBuilder AddInstrumentation<T>(
        this LoggerProviderBuilder loggerProviderBuilder,
        Func<IServiceProvider, T> instrumentationFactory)
        where T : class
    {
        Guard.ThrowIfNull(instrumentationFactory);

        loggerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => instrumentationFactory(sp));
        });

        return loggerProviderBuilder;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <remarks><inheritdoc cref="AddInstrumentation{T}(LoggerProviderBuilder, T)" path="/remarks"/></remarks>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="instrumentationFactory">Instrumentation factory.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
#else
    /// <summary>
    /// Adds instrumentation to the provider.
    /// </summary>
    /// <typeparam name="T">Instrumentation type.</typeparam>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="instrumentationFactory">Instrumentation factory.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
#endif
    public static LoggerProviderBuilder AddInstrumentation<T>(
        this LoggerProviderBuilder loggerProviderBuilder,
        Func<IServiceProvider, LoggerProvider, T> instrumentationFactory)
        where T : class
    {
        Guard.ThrowIfNull(instrumentationFactory);

        loggerProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            if (builder is ILoggerProviderBuilder iLoggerProviderBuilder
                && iLoggerProviderBuilder.Provider != null)
            {
                builder.AddInstrumentation(() => instrumentationFactory(sp, iLoggerProviderBuilder.Provider));
            }
        });

        return loggerProviderBuilder;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="IServiceCollection"/> where logging services are configured.
    /// </summary>
    /// <remarks>
    /// <para><inheritdoc cref="AddInstrumentation{T}(LoggerProviderBuilder, T)" path="/remarks"/></para>
    /// Note: Logging services are only available during the application
    /// configuration phase.
    /// </remarks>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
#else
    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="IServiceCollection"/> where logging services are configured.
    /// </summary>
    /// <remarks>
    /// Note: Logging services are only available during the application
    /// configuration phase.
    /// </remarks>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
#endif
    public static LoggerProviderBuilder ConfigureServices(
        this LoggerProviderBuilder loggerProviderBuilder,
        Action<IServiceCollection> configure)
    {
        if (loggerProviderBuilder is ILoggerProviderBuilder iLoggerProviderBuilder)
        {
            iLoggerProviderBuilder.ConfigureServices(configure);
        }

        return loggerProviderBuilder;
    }

    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="LoggerProviderBuilder"/> once the application <see
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
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    internal static LoggerProviderBuilder ConfigureBuilder(
        this LoggerProviderBuilder loggerProviderBuilder,
        Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        if (loggerProviderBuilder is IDeferredLoggerProviderBuilder deferredLoggerProviderBuilder)
        {
            deferredLoggerProviderBuilder.Configure(configure);
        }

        return loggerProviderBuilder;
    }
}
