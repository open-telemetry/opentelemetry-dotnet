// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using static OpenTelemetry.Internal.SelfDiagnostics;

namespace OpenTelemetry;

/// <summary>
/// OpenTelemetry helper.
/// </summary>
public static class Sdk
{
    static Sdk()
    {
        Propagators.DefaultTextMapPropagator = DefaultTextMapPropagator;

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;

#if NET
        if (!OperatingSystem.IsBrowser())
        {
            EnsureInitialized();
        }
#else
        EnsureInitialized();
#endif

        var sdkAssembly = typeof(Sdk).Assembly;
        InformationalVersion = sdkAssembly.GetPackageVersion();
    }

    /// <summary>
    /// Gets a value indicating whether instrumentation is suppressed (disabled).
    /// </summary>
    public static bool SuppressInstrumentation => SuppressInstrumentationScope.IsSuppressed;

    /// <summary>
    /// Gets the default <see cref="TextMapPropagator"/> used by the SDK.
    /// </summary>
    /// <remarks>
    /// The default propagator is a composite of the W3C Trace Context and W3C Baggage
    /// propagators. The same instance is returned every time.
    /// <para/>
    /// This is not necessarily the propagator currently in use, which is
    /// available from <see cref="Propagators.DefaultTextMapPropagator"/> and can be
    /// replaced using <see cref="SetDefaultTextMapPropagator(TextMapPropagator)"/>.
    /// This property always returns the propagator the SDK installs by default, so
    /// it can be used to restore the SDK default after a custom propagator has been
    /// set, or to determine whether the current propagator has been replaced.
    /// </remarks>
    public static TextMapPropagator DefaultTextMapPropagator { get; } = new CompositeTextMapPropagator(
    [
        new TraceContextPropagator(),
        new BaggagePropagator(),
    ]);

    internal static string InformationalVersion { get; }

    /// <summary>
    /// Sets the Default TextMapPropagator.
    /// </summary>
    /// <param name="textMapPropagator">TextMapPropagator to be set as default.</param>
    public static void SetDefaultTextMapPropagator(TextMapPropagator textMapPropagator)
    {
        Guard.ThrowIfNull(textMapPropagator);

        Propagators.DefaultTextMapPropagator = textMapPropagator;
    }

    /// <summary>
    /// Creates a <see cref="MeterProviderBuilder"/> which is used to build
    /// a <see cref="MeterProvider"/>. In a typical application, a single
    /// <see cref="MeterProvider"/> is created at application startup and disposed
    /// at application shutdown. It is important to ensure that the provider is not
    /// disposed too early.
    /// </summary>
    /// <returns><see cref="MeterProviderBuilder"/> instance, which is used to build a <see cref="MeterProvider"/>.</returns>
    public static MeterProviderBuilder CreateMeterProviderBuilder()
        => new MeterProviderBuilderBase();

    /// <summary>
    /// Creates a <see cref="TracerProviderBuilder"/> which is used to build
    /// a <see cref="TracerProvider"/>. In a typical application, a single
    /// <see cref="TracerProvider"/> is created at application startup and disposed
    /// at application shutdown. It is important to ensure that the provider is not
    /// disposed too early.
    /// </summary>
    /// <returns><see cref="TracerProviderBuilder"/> instance, which is used to build a <see cref="TracerProvider"/>.</returns>
    public static TracerProviderBuilder CreateTracerProviderBuilder()
        => new TracerProviderBuilderBase();

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Creates a <see cref="LoggerProviderBuilder"/> which is used to build
    /// a <see cref="LoggerProvider"/>. In a typical application, a single
    /// <see cref="LoggerProvider"/> is created at application startup and
    /// disposed at application shutdown. It is important to ensure that the
    /// provider is not disposed too early.
    /// </summary>
    /// <remarks><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</remarks>
    /// <returns><see cref="LoggerProviderBuilder"/> instance, which is used
    /// to build a <see cref="LoggerProvider"/>.</returns>
    [Experimental(DiagnosticDefinitions.LogsExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
    public
#else
    /// <summary>
    /// Creates a <see cref="LoggerProviderBuilder"/> which is used to build
    /// a <see cref="LoggerProvider"/>. In a typical application, a single
    /// <see cref="LoggerProvider"/> is created at application startup and
    /// disposed at application shutdown. It is important to ensure that the
    /// provider is not disposed too early.
    /// </summary>
    /// <returns><see cref="LoggerProviderBuilder"/> instance, which is used
    /// to build a <see cref="LoggerProvider"/>.</returns>
    internal
#endif
            static LoggerProviderBuilder CreateLoggerProviderBuilder()
        => new LoggerProviderBuilderBase();
}
