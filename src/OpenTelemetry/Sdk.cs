// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
#if EXPOSE_EXPERIMENTAL_FEATURES && NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
using System.Reflection;
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

        var assemblyInformationalVersion = typeof(Sdk).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        InformationalVersion = ParseAssemblyInformationalVersion(assemblyInformationalVersion);
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
#if NET8_0_OR_GREATER
    [Experimental(DiagnosticDefinitions.LoggerProviderExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
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

    internal static string ParseAssemblyInformationalVersion(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            informationalVersion = "1.0.0";
        }

        /*
         * InformationalVersion will be in the following format:
         *   {majorVersion}.{minorVersion}.{patchVersion}.{pre-release label}.{pre-release version}.{gitHeight}+{Git SHA of current commit}
         * Ex: 1.5.0-alpha.1.40+807f703e1b4d9874a92bd86d9f2d4ebe5b5d52e4
         * The following parts are optional: pre-release label, pre-release version, git height, Git SHA of current commit
         */

        var indexOfPlusSign = informationalVersion!.IndexOf('+');
        return indexOfPlusSign > 0
            ? informationalVersion.Substring(0, indexOfPlusSign)
            : informationalVersion;
    }
}
