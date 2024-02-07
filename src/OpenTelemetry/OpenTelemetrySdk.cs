// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
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
        Debug.Assert(configure != null, "configure was null");

        var services = new ServiceCollection();

        var builder = new OpenTelemetrySdkBuilder(services);

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
    /// Gets the <see cref="IServiceProvider"/> containing SDK services.
    /// </summary>
    public IServiceProvider Services => this.serviceProvider;

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

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets the <see cref="Logs.LoggerProvider"/>.
    /// </summary>
    /// <remarks>
    /// <para><b>WARNING</b>: This is an experimental API which might change or
    /// be removed in the future. Use at your own risk.</para>
    /// Note: The default <see cref="LoggerProvider"/> will be a no-op instance.
    /// Call <see
    /// cref="OpenTelemetryBuilderSdkExtensions.WithLogging(IOpenTelemetryBuilder)"/> to
    /// enable logging.
    /// </remarks>
#if NET8_0_OR_GREATER
    [Experimental(DiagnosticDefinitions.LoggerProviderExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
    public LoggerProvider LoggerProvider { get; }
#else
    /// <summary>
    /// Gets the <see cref="Logs.LoggerProvider"/>.
    /// </summary>
    /// <remarks>
    /// Note: The default <see cref="LoggerProvider"/> will be a no-op instance.
    /// Call <see
    /// cref="OpenTelemetryBuilderSdkExtensions.WithLogging(IOpenTelemetryBuilder)"/> to
    /// enable logging.
    /// </remarks>
    internal LoggerProvider LoggerProvider { get; }
#endif

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

    private sealed class OpenTelemetrySdkBuilder : IOpenTelemetryBuilder
    {
        public OpenTelemetrySdkBuilder(IServiceCollection services)
        {
            Debug.Assert(services != null, "services was null");

            services!.AddOpenTelemetrySharedProviderBuilderServices();

            this.Services = services!;
        }

        public IServiceCollection Services { get; }
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
