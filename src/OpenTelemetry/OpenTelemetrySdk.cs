// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace OpenTelemetry;

/// <summary>
/// Contains methods for configuring the OpenTelemetry SDK and accessing
/// logging, metrics, and tracing providers.
/// </summary>
public sealed class OpenTelemetrySdk : IDisposable
{
    private readonly ServiceProvider serviceProvider;

    private OpenTelemetrySdk(
        Action<IOpenTelemetryBuilder> configure)
    {
        var services = new ServiceCollection();

        var builder = new OpenTelemetrySdkBuilder(services);

        configure(builder);

        this.serviceProvider = services.BuildServiceProvider();

        this.LoggerProvider = (LoggerProvider?)this.serviceProvider.GetService(typeof(LoggerProvider))
            ?? new NoopLoggerProvider();
        this.MeterProvider = (MeterProvider?)this.serviceProvider.GetService(typeof(MeterProvider))
            ?? new NoopMeterProvider();
        this.TracerProvider = (TracerProvider?)this.serviceProvider.GetService(typeof(TracerProvider))
            ?? new NoopTracerProvider();
    }

    /// <summary>
    /// Gets the <see cref="Logs.LoggerProvider"/>.
    /// </summary>
    /// <remarks>
    /// Note: The default <see cref="LoggerProvider"/> will be a no-op instance.
    /// Call <see
    /// cref="OpenTelemetryBuilderSdkExtensions.WithLogging(IOpenTelemetryBuilder)"/> to
    /// enable logging.
    /// </remarks>
    public LoggerProvider LoggerProvider { get; }

    /// <summary>
    /// Gets the <see cref="Metrics.MeterProvider"/>.
    /// </summary>
    /// <remarks>
    /// Note: The default <see cref="MeterProvider"/> will be a no-op instance.
    /// Call <see
    /// cref="OpenTelemetryBuilderSdkExtensions.WithMetrics(IOpenTelemetryBuilder)"/>
    /// to enable metrics.
    /// </remarks>
    public MeterProvider MeterProvider { get; }

    /// <summary>
    /// Gets the <see cref="Trace.TracerProvider"/>.
    /// </summary>
    /// <remarks>
    /// Note: The default <see cref="TracerProvider"/> will be a no-op instance.
    /// Call <see
    /// cref="OpenTelemetryBuilderSdkExtensions.WithTracing(IOpenTelemetryBuilder)"/>
    /// to enable tracing.
    /// </remarks>
    public TracerProvider TracerProvider { get; }

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/> containing SDK services.
    /// </summary>
    internal IServiceProvider Services => this.serviceProvider;

    /// <summary>
    /// Create an <see cref="OpenTelemetrySdk"/> instance.
    /// </summary>
    /// <param name="configure"><see cref="IOpenTelemetryBuilder"/> configuration delegate.</param>
    /// <returns>Created <see cref="OpenTelemetrySdk"/>.</returns>
    public static OpenTelemetrySdk Create(
        Action<IOpenTelemetryBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return new(configure);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.serviceProvider.Dispose();
    }

    internal sealed class NoopLoggerProvider : LoggerProvider
    {
    }

    internal sealed class NoopMeterProvider : MeterProvider
    {
    }

    internal sealed class NoopTracerProvider : TracerProvider
    {
    }

    private sealed class OpenTelemetrySdkBuilder : IOpenTelemetryBuilder
    {
        public OpenTelemetrySdkBuilder(IServiceCollection services)
        {
            services.AddOpenTelemetrySharedProviderBuilderServices();

            this.Services = services;
        }

        public IServiceCollection Services { get; }
    }
}
