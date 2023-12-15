// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
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
            builder => builder.ConfigureResource(configure));

        this.Services.ConfigureOpenTelemetryTracerProvider(
            builder => builder.ConfigureResource(configure));

        this.Services.ConfigureOpenTelemetryLoggerProvider(
            builder => builder.ConfigureResource(configure));

        return this;
    }

    /// <summary>
    /// Adds metric services into the builder.
    /// </summary>
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
        OpenTelemetryMetricsBuilderExtensions.RegisterMetricsListener(
            this.Services,
            configure);

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
    /// <para><b>WARNING</b>: This is an experimental API which might change or
    /// be removed in the future. Use at your own risk.</para>
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
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
#if NET8_0_OR_GREATER
    [Experimental(DiagnosticDefinitions.LoggerProviderExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
    public
#else
    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
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
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    internal
#endif
        OpenTelemetryBuilder WithLogging()
        => this.WithLogging(configureBuilder: null, configureOptions: null);

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging()" path="/remarks"/></remarks>
    /// <param name="configure"><see cref="LoggerProviderBuilder"/>
    /// configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
#if NET8_0_OR_GREATER
    [Experimental(DiagnosticDefinitions.LoggerProviderExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
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

        return this.WithLogging(configureBuilder: configure, configureOptions: null);
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging()" path="/remarks"/></remarks>
    /// <param name="configureBuilder">Optional <see
    /// cref="LoggerProviderBuilder"/> configuration callback.</param>
    /// <param name="configureOptions">Optional <see
    /// cref="OpenTelemetryLoggerOptions"/> configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
#if NET8_0_OR_GREATER
    [Experimental(DiagnosticDefinitions.LoggerProviderExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
    public
#else
    /// <summary>
    /// Adds logging services into the builder.
    /// </summary>
    /// <remarks><inheritdoc cref="WithLogging()" path="/remarks"/></remarks>
    /// <param name="configureBuilder">Optional <see
    /// cref="LoggerProviderBuilder"/> configuration callback.</param>
    /// <param name="configureOptions">Optional <see
    /// cref="OpenTelemetryLoggerOptions"/> configuration callback.</param>
    /// <returns>The supplied <see cref="OpenTelemetryBuilder"/> for chaining
    /// calls.</returns>
    internal
#endif
        OpenTelemetryBuilder WithLogging(
            Action<LoggerProviderBuilder>? configureBuilder,
            Action<OpenTelemetryLoggerOptions>? configureOptions)
    {
        this.Services.AddLogging(
            logging => logging.UseOpenTelemetry(configureBuilder, configureOptions));

        return this;
    }
}
