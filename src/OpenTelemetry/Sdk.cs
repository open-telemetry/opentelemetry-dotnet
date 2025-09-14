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

namespace OpenTelemetry;

/// <summary>
/// OpenTelemetry helper.
/// </summary>
public static class Sdk
{
    static Sdk()
    {
        Propagators.DefaultTextMapPropagator = new CompositeTextMapPropagator(new TextMapPropagator[]
        {
            new TraceContextPropagator(),
            new BaggagePropagator(),
        });

        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
        SelfDiagnostics.EnsureInitialized();

        var sdkAssembly = typeof(Sdk).Assembly;
        InformationalVersion = sdkAssembly.GetPackageVersion();
    }

    /// <summary>
    /// Gets a value indicating whether instrumentation is suppressed (disabled).
    /// </summary>
    public static bool SuppressInstrumentation => SuppressInstrumentationScope.IsSuppressed;

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
    {
        return new MeterProviderBuilderBase();
    }

    /// <summary>
    /// Creates a <see cref="TracerProviderBuilder"/> which is used to build
    /// a <see cref="TracerProvider"/>. In a typical application, a single
    /// <see cref="TracerProvider"/> is created at application startup and disposed
    /// at application shutdown. It is important to ensure that the provider is not
    /// disposed too early.
    /// </summary>
    /// <returns><see cref="TracerProviderBuilder"/> instance, which is used to build a <see cref="TracerProvider"/>.</returns>
    public static TracerProviderBuilder CreateTracerProviderBuilder()
    {
        return new TracerProviderBuilderBase();
    }

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
    {
        return new LoggerProviderBuilderBase();
    }
}
