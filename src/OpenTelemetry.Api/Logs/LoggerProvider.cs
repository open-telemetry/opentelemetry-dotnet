// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
#endif
#if NET8_0_OR_GREATER
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// LoggerProvider is the entry point of the OpenTelemetry API. It provides access to <see cref="Logger"/>.
/// </summary>
/// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
#if NET8_0_OR_GREATER
[Experimental(DiagnosticDefinitions.LoggerProviderExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
/// <summary>
/// LoggerProvider is the entry point of the OpenTelemetry API. It provides access to <see cref="Logger"/>.
/// </summary>
internal
#endif
    class LoggerProvider : BaseProvider
{
    private static readonly NoopLogger NoopLogger = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerProvider"/> class.
    /// </summary>
    protected LoggerProvider()
    {
    }

    /// <summary>
    /// Gets a logger.
    /// </summary>
    /// <returns><see cref="Logger"/> instance.</returns>
#if NET8_0_OR_GREATER
    [Experimental(DiagnosticDefinitions.LogBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
    public Logger GetLogger()
        => this.GetLogger(name: null, version: null);

    /// <summary>
    /// Gets a logger with the given name.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
#if NET8_0_OR_GREATER
    [Experimental(DiagnosticDefinitions.LogBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
    public Logger GetLogger(string? name)
        => this.GetLogger(name, version: null);

    /// <summary>
    /// Gets a logger with the given name and version.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <param name="version">Optional version of the instrumentation library.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
#if NET8_0_OR_GREATER
    [Experimental(DiagnosticDefinitions.LogBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
    public Logger GetLogger(string? name, string? version)
    {
        if (!this.TryCreateLogger(name, out var logger))
        {
            return NoopLogger;
        }

        logger!.SetInstrumentationScope(version);

        return logger;
    }

    /// <summary>
    /// Try to create a logger with the given name.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <param name="logger"><see cref="Logger"/>.</param>
    /// <returns><see langword="true"/> if the logger was created.</returns>
#if NET8_0_OR_GREATER
    [Experimental(DiagnosticDefinitions.LogBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
    protected virtual bool TryCreateLogger(
        string? name,
#if NETSTANDARD2_1_OR_GREATER || NET6_0_OR_GREATER
        [NotNullWhen(true)]
#endif
        out Logger? logger)
    {
        logger = null;
        return false;
    }
}
