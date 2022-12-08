// <copyright file="OpenTelemetryDependencyInjectionMeterProviderBuilderExtensions.cs" company="OpenTelemetry Authors">
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
    public static MeterProviderBuilder AddInstrumentation<T>(this MeterProviderBuilder meterProviderBuilder)
        where T : class
    {
        meterProviderBuilder.ConfigureServices(services => services.TryAddSingleton<T>());

        meterProviderBuilder.ConfigureBuilder((sp, builder) =>
        {
            builder.AddInstrumentation(() => sp.GetRequiredService<T>());
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
    /// cref="IServiceCollection"/> where tracing services are configured.
    /// </summary>
    /// <remarks>
    /// Note: Tracing services are only available during the application
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
    /// <param name="meterProviderBuilder"><see cref="MeterProviderBuilder"/>.</param>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="MeterProviderBuilder"/> for chaining.</returns>
    public static MeterProviderBuilder ConfigureBuilder(
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
