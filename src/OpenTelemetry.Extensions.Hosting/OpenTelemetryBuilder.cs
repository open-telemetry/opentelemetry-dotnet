// <copyright file="OpenTelemetryBuilder.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// Contains methods for configuring the OpenTelemetry SDK inside an <see
/// cref="IServiceCollection"/>.
/// </summary>
public sealed class OpenTelemetryBuilder
{
    internal OpenTelemetryBuilder(IServiceCollection services)
    {
        Guard.ThrowIfNull(services);

        services.AddOpenTelemetrySharedProviderBuilderServices();

        this.Services = services;
    }

    /// <summary>
    /// Gets the <see cref="IServiceCollection"/> behind the builder.
    /// </summary>
    public IServiceCollection Services { get; }

    /// <summary>
    /// Registers an action to configure the <see cref="ResourceBuilder"/>s used
    /// by tracing, metrics, and logging.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied sequentially.
    /// </remarks>
    /// <param name="configure"><see cref="ResourceBuilder"/> configuration
    /// action.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder ConfigureResource(
        Action<ResourceBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        this.Services.ConfigureOpenTelemetryMeterProvider(
            (sp, builder) => builder.ConfigureResource(configure));

        this.Services.ConfigureOpenTelemetryTracerProvider(
            (sp, builder) => builder.ConfigureResource(configure));

        this.Services.ConfigureOpenTelemetryLoggerProvider(
            (sp, builder) => builder.ConfigureResource(configure));

        return this;
    }

    /// <summary>
    /// Adds metric services into the builder.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="MeterProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder WithMetrics()
        => this.WithMetrics(b => { });

    /// <summary>
    /// Adds metric services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithMetrics()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="MeterProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder WithMetrics(Action<MeterProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        var builder = new MeterProviderBuilderBase(this.Services);

        configure(builder);

        return this;
    }

    /// <summary>
    /// Adds metric services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithMetrics()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="MeterProviderBuilder"/>
    /// deferred configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder WithMetrics(Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        this.WithMetrics();

        this.Services.ConfigureOpenTelemetryMeterProvider(configure);

        return this;
    }

    /// <summary>
    /// Adds tracing services into the builder.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="TracerProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder WithTracing()
        => this.WithTracing(b => { });

    /// <summary>
    /// Adds tracing services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithTracing()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="TracerProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder WithTracing(Action<TracerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        var builder = new TracerProviderBuilderBase(this.Services);

        configure(builder);

        return this;
    }

    /// <summary>
    /// Adds tracing services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithTracing()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="TracerProviderBuilder"/>
    /// deferred configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public OpenTelemetryBuilder WithTracing(Action<IServiceProvider, TracerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        this.WithTracing();

        this.Services.ConfigureOpenTelemetryTracerProvider(configure);

        return this;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks>
    /// <para><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</para>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="LoggerProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public
#else
    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="LoggerProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    internal
#endif
        OpenTelemetryBuilder WithLogging()
        => this.WithLogging(b => { });

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="LoggerProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public
#else
    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="LoggerProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    internal
#endif
        OpenTelemetryBuilder WithLogging(Action<LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        var builder = new LoggerProviderBuilderBase(this.Services);

        configure(builder);

        return this;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="LoggerProviderBuilder"/>
    /// deferred configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public
#else
    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="LoggerProviderBuilder"/>
    /// deferred configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    internal
#endif
        OpenTelemetryBuilder WithLogging(Action<IServiceProvider, LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        this.WithLogging();

        this.Services.ConfigureOpenTelemetryLoggerProvider(configure);

        return this;
    }
}
