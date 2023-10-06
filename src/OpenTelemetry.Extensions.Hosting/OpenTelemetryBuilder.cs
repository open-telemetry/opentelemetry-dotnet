// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
}
