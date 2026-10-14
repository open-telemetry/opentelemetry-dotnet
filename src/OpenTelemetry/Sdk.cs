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
        Propagators.DefaultTextMapPropagator = CreateDefaultTextMapPropagator();

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

    internal static string InformationalVersion { get; }

    /// <summary>
    /// Creates the SDK's default <see cref="TextMapPropagator"/>, which propagates
    /// W3C Trace Context and Baggage.
    /// </summary>
    /// <remarks>
    /// This method creates a new instance without changing the current global
    /// propagator. Use <see cref="SetDefaultTextMapPropagator"/> to set it as the
    /// global default.
    /// </remarks>
    /// <returns>A new instance of the SDK's default <see cref="TextMapPropagator"/>.</returns>
    public static TextMapPropagator CreateDefaultTextMapPropagator()
        => new CompositeTextMapPropagator(
        [
            new TraceContextPropagator(),
            new BaggagePropagator(),
        ]);

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
    [Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
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
