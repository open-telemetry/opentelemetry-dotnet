// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Exporter.Formatting;

namespace OpenTelemetry.Exporter;

public class ConsoleExporterOptions
{
    private const string DefaultTimestampFormat = "HH:mm:ss ";

    // By default use a shared system console, so that exporters can synchronise on the same lock
    private static SystemConsole sharedSystemConsole = new();

    /// <summary>
    /// Gets or sets the output targets for the console exporter.
    /// </summary>
    public ConsoleExporterOutputTargets Targets { get; set; } = ConsoleExporterOutputTargets.Console;

    /// <summary>
    /// Gets or sets the formatter to use. Default is the 'Detail' formatter for a verbose output with all fields; use 'Compact' for a single line format.
    /// </summary>
    public string Formatter { get; set; } = FormatterFactory.Detail;

    /// <summary>
    /// Gets or sets the timestamp format string. If empty, no timestamp is output.
    /// </summary>
    public string TimestampFormat { get; set; } = DefaultTimestampFormat;

    /// <summary>
    /// Gets or sets a value indicating whether to use UTC timestamps. If false, local time is used.
    /// </summary>
    public bool UseUtcTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the console to use for output. Defaults to SystemConsole.
    /// </summary>
    internal IConsole Console { get; set; } = sharedSystemConsole;
}
