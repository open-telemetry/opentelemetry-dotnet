// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_1_OR_GREATER || NET || EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
#endif
using OpenTelemetry.Internal;

namespace OpenTelemetry.Logs;

/// <summary>
/// LoggerProvider is the entry point of the OpenTelemetry API. It provides access to <see cref="Logger"/>.
/// </summary>
public class LoggerProvider : BaseProvider
{
    private static readonly NoopLogger NoopLogger = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="LoggerProvider"/> class.
    /// </summary>
    protected LoggerProvider()
    {
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets a logger.
    /// </summary>
    /// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
    /// <returns><see cref="Logger"/> instance.</returns>
    [Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
    public
#else
    internal
#endif
        Logger GetLogger()
        => this.GetLogger(name: null, version: null);

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets a logger with the given name.
    /// </summary>
    /// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
    [Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
    public
#else
    internal
#endif
        Logger GetLogger(string? name)
        => this.GetLogger(name, version: null);

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets a logger with the given name and version.
    /// </summary>
    /// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <param name="version">Optional version of the instrumentation library.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
    [Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
    public
#else
    internal
#endif
        Logger GetLogger(string? name, string? version)
        => this.GetLogger(new LoggerOptions() { Name = name, Version = version });

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Gets a logger with the given name and version.
    /// </summary>
    /// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
    /// <param name="options">The options to use to create the logger.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
    [Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
    public
#else
    internal
#endif
        Logger GetLogger(LoggerOptions options)
    {
        Guard.ThrowIfNull(options);

        if (!this.TryCreateLogger(options, out var logger))
        {
            return NoopLogger;
        }

#if NET
        logger.SetInstrumentationScope(options?.Version, options?.SchemaUrl);
#else
        logger!.SetInstrumentationScope(options?.Version, options?.SchemaUrl);
#endif

        return logger;
    }

#if EXPOSE_EXPERIMENTAL_FEATURES
    /// <summary>
    /// Try to create a logger with the given name.
    /// </summary>
    /// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
    /// <param name="options">The options to use to create the logger.</param>
    /// <param name="logger">If successful, contains the created <see cref="Logger"/>.</param>
    /// <returns><see langword="true"/> if the logger was created.</returns>
    [Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
    protected
#else
    internal
#endif
        virtual bool TryCreateLogger(
        LoggerOptions options,
#if NETSTANDARD2_1_OR_GREATER || NET
        [NotNullWhen(true)]
#endif
        out Logger? logger)
    {
        logger = null;
        return false;
    }
}
