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

    internal OpenTelemetrySdk(
        Action<OpenTelemetryBuilder> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        var services = new ServiceCollection();

        var builder = new OpenTelemetryBuilder(services);

        configure!(builder);

        this.serviceProvider = services.BuildServiceProvider();

        this.LoggerProvider = (LoggerProvider?)this.serviceProvider.GetService(typeof(LoggerProvider))
            ?? new NoopLoggerProvider();
        this.MeterProvider = (MeterProvider?)this.serviceProvider.GetService(typeof(MeterProvider))
            ?? new NoopMeterProvider();
        this.TracerProvider = (TracerProvider?)this.serviceProvider.GetService(typeof(TracerProvider))
            ?? new NoopTracerProvider();
    }

    /// <summary>
    /// Gets the <see cref="IServiceProvider"/>.
    /// </summary>
    public IServiceProvider Services => this.serviceProvider;

    /// <summary>
    /// Gets the <see cref="Metrics.MeterProvider"/>.
    /// </summary>
    /// <remarks>
    /// Note: The default <see cref="MeterProvider"/> will be a no-op instance.
    /// Call <see cref="OpenTelemetryBuilder.WithMetrics()"/> or <see
    /// cref="OpenTelemetryBuilder.WithMetrics(Action{MeterProviderBuilder})"/>
    /// to enable metrics.
    /// </remarks>
    public MeterProvider MeterProvider { get; }

    /// <summary>
    /// Gets the <see cref="Trace.TracerProvider"/>.
    /// </summary>
    /// <remarks>
    /// Note: The default <see cref="TracerProvider"/> will be a no-op instance.
    /// Call <see cref="OpenTelemetryBuilder.WithTracing()"/> or <see
    /// cref="OpenTelemetryBuilder.WithTracing(Action{TracerProviderBuilder})"/>
    /// to enable tracing.
    /// </remarks>
    public TracerProvider TracerProvider { get; }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets the <see cref="Logs.LoggerProvider"/>.
    /// </summary>
    /// <remarks>
    /// Note: The default <see cref="LoggerProvider"/> will be a no-op instance.
    /// Call <see cref="OpenTelemetryBuilder.WithLogging()"/> or <see
    /// cref="OpenTelemetryBuilder.WithLogging(Action{LoggerProviderBuilder})"/>
    /// to enable logging.
    /// </remarks>
    public LoggerProvider LoggerProvider { get; }
#else
    /// <summary>
    /// Gets the <see cref="Logs.LoggerProvider"/>.
    /// </summary>
    /// <remarks>
    /// Note: The default <see cref="LoggerProvider"/> will be a no-op instance.
    /// Call <see cref="OpenTelemetryBuilder.WithLogging()"/> or <see
    /// cref="OpenTelemetryBuilder.WithLogging(Action{LoggerProviderBuilder})"/>
    /// to enable logging.
    /// </remarks>
    internal LoggerProvider LoggerProvider { get; }
#endif

    /// <summary>
    /// Create an <see cref="OpenTelemetrySdk"/> instance.
    /// </summary>
    /// <returns>Created <see cref="OpenTelemetrySdk"/>.</returns>
    public static OpenTelemetrySdk Create()
        => Create(b => { });

    /// <summary>
    /// Create an <see cref="OpenTelemetrySdk"/> instance.
    /// </summary>
    /// <param name="configure"><see cref="OpenTelemetryBuilder"/> configuration delegate.</param>
    /// <returns>Created <see cref="OpenTelemetrySdk"/>.</returns>
    public static OpenTelemetrySdk Create(
        Action<OpenTelemetryBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        return new(configure);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.serviceProvider.Dispose();
    }

    private sealed class NoopLoggerProvider : LoggerProvider
    {
    }

    private sealed class NoopMeterProvider : MeterProvider
    {
    }

    private sealed class NoopTracerProvider : TracerProvider
    {
    }
}
