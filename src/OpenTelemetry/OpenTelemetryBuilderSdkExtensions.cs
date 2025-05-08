// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// Contains methods for extending the <see cref="IOpenTelemetryBuilder"/> interface.
/// </summary>
public static class OpenTelemetryBuilderSdkExtensions
{
    /// <summary>
    /// Registers an action to configure the <see cref="ResourceBuilder"/>s used
    /// by tracing, metrics, and logging.
    /// </summary>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Each registered configuration action will be applied sequentially.
    /// </remarks>
    /// <param name="configure"><see cref="ResourceBuilder"/> configuration
    /// action.</param>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder ConfigureResource(
        this IOpenTelemetryBuilder builder,
        Action<ResourceBuilder> configure)
    {
        Guard.ThrowIfNull(builder);
        Guard.ThrowIfNull(configure);

#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        builder.Services.ConfigureOpenTelemetryMeterProvider(
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1
            builder => builder.ConfigureResource(configure));

        builder.Services.ConfigureOpenTelemetryTracerProvider(
            builder => builder.ConfigureResource(configure));

        builder.Services.ConfigureOpenTelemetryLoggerProvider(
            builder => builder.ConfigureResource(configure));

        return builder;
    }

    /// <summary>
    /// Adds metric services into the builder.
    /// </summary>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="MeterProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.</item>
    /// <item>This method automatically registers an <see
    /// cref="IMetricsListener"/> named 'OpenTelemetry' into the <see
    /// cref="IServiceCollection"/>.</item>
    /// </list>
    /// </remarks>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder WithMetrics(
        this IOpenTelemetryBuilder builder)
        => WithMetrics(builder, b => { });

    /// <summary>
    /// Adds metric services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithMetrics(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="configure"><see cref="MeterProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder WithMetrics(
        this IOpenTelemetryBuilder builder,
        Action<MeterProviderBuilder> configure)
    {
        OpenTelemetryMetricsBuilderExtensions.RegisterMetricsListener(
#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
            builder.Services,
            configure);
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1

        return builder;
    }

    /// <summary>
    /// Adds tracing services into the builder.
    /// </summary>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <remarks>
    /// Note: This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="TracerProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.
    /// </remarks>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder WithTracing(this IOpenTelemetryBuilder builder)
        => WithTracing(builder, b => { });

    /// <summary>
    /// Adds tracing services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithTracing(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="configure"><see cref="TracerProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder WithTracing(
        this IOpenTelemetryBuilder builder,
        Action<TracerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

#pragma warning disable CA1062 // Validate arguments of public methods - needed for netstandard2.1
        var tracerProviderBuilder = new TracerProviderBuilderBase(builder.Services);

        configure(tracerProviderBuilder);
#pragma warning restore CA1062 // Validate arguments of public methods - needed for netstandard2.1

        return builder;
    }

    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <remarks>
    /// Notes:
    /// <list type="bullet">
    /// <item>This is safe to be called multiple times and by library authors.
    /// Only a single <see cref="LoggerProvider"/> will be created for a given
    /// <see cref="IServiceCollection"/>.</item>
    /// <item>This method automatically registers an <see
    /// cref="ILoggerProvider"/> named 'OpenTelemetry' into the <see
    /// cref="IServiceCollection"/>.</item>
    /// </list>
    /// </remarks>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder WithLogging(this IOpenTelemetryBuilder builder)
        => WithLogging(builder, configureBuilder: null, configureOptions: null);

    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="configure"><see cref="LoggerProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder WithLogging(
        this IOpenTelemetryBuilder builder,
        Action<LoggerProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return WithLogging(builder, configureBuilder: configure, configureOptions: null);
    }

    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging(IOpenTelemetryBuilder)" path="/remarks"/></remarks>
    /// <param name="builder"><see cref="IOpenTelemetryBuilder"/>.</param>
    /// <param name="configureBuilder">Optional <see
    /// cref="LoggerProviderBuilder"/> configuration callback.</param>
    /// <param name="configureOptions">Optional <see
    /// cref="OpenTelemetryLoggerOptions"/> configuration callback. <see
    /// cref="OpenTelemetryLoggerOptions"/> are used by the <see
    /// cref="ILoggerProvider"/> named 'OpenTelemetry' automatically registered
    /// by this method.</param>
    /// <returns>The supplied <see cref="IOpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    public static IOpenTelemetryBuilder WithLogging(
        this IOpenTelemetryBuilder builder,
        Action<LoggerProviderBuilder>? configureBuilder,
        Action<OpenTelemetryLoggerOptions>? configureOptions)
    {
        builder.Services.AddLogging(
            logging => logging.UseOpenTelemetry(configureBuilder, configureOptions));

        return builder;
    }
}
