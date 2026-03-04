// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NETSTANDARD2_1_OR_GREATER || NET
using System.Diagnostics.CodeAnalysis;
#endif

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

    /// <summary>
    /// Gets a logger.
    /// </summary>
    /// <returns><see cref="Logger"/> instance.</returns>
    public Logger GetLogger()
        => this.GetLogger(name: null, version: null);

    /// <summary>
    /// Gets a logger with the given name.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
    public Logger GetLogger(string? name)
        => this.GetLogger(name, version: null);

    /// <summary>
    /// Gets a logger with the given name and version.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    /// <param name="version">Optional version of the instrumentation library.</param>
    /// <returns><see cref="Logger"/> instance.</returns>
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
    protected virtual bool TryCreateLogger(
        string? name,
#if NETSTANDARD2_1_OR_GREATER || NET
        [NotNullWhen(true)]
#endif
        out Logger? logger)
    {
        logger = null;
        return false;
    }
}
