// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;

namespace OpenTelemetry.Internal;

/// <summary>
/// A valid legacy <c>OTEL_DIAGNOSTICS.json</c> configuration.
/// </summary>
internal readonly record struct SelfDiagnosticsLegacyConfiguration
{
    internal SelfDiagnosticsLegacyConfiguration(
        string configFilePath,
        string logDirectory,
        int fileSizeKilobytes,
        EventLevel logLevel,
        string rawLogLevel,
        bool formatMessageSpecified)
    {
        this.ConfigFilePath = configFilePath;
        this.LogDirectory = logDirectory;
        this.FileSizeKilobytes = fileSizeKilobytes;
        this.LogLevel = logLevel;
        this.RawLogLevel = rawLogLevel;
        this.FormatMessageSpecified = formatMessageSpecified;
    }

    /// <summary>
    /// Gets the path to the configuration file.
    /// </summary>
    internal string ConfigFilePath { get; }

    /// <summary>
    /// Gets the directory where log files are stored.
    /// </summary>
    internal string LogDirectory { get; }

    /// <summary>
    /// Gets the raw, unclamped file size in kilobytes.
    /// </summary>
    internal int FileSizeKilobytes { get; }

    /// <summary>
    /// Gets the log level.
    /// </summary>
    internal EventLevel LogLevel { get; }

    /// <summary>
    /// Gets the raw log level as specified in the configuration.
    /// </summary>
    internal string RawLogLevel { get; }

    /// <summary>
    /// Gets a value indicating whether the format message option was specified.
    /// </summary>
    internal bool FormatMessageSpecified { get; }
}
