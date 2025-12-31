// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

public class ConsoleExporterOptions
{
    // By default use a shared system console, so that exporters can synchronise on the same lock
    private static SystemConsole sharedSystemConsole = new();

    /// <summary>
    /// Gets or sets the output targets for the console exporter.
    /// </summary>
    public ConsoleExporterOutputTargets Targets { get; set; } = ConsoleExporterOutputTargets.Console;

    /// <summary>
    /// Gets or sets the formatter to use. Default is the Simple formatter; use KeyValue for old format.
    /// </summary>
    public string Formatter { get; set; } = "Simple";

    /// <summary>
    /// Gets or sets the timestamp format string. If empty, no timestamp is output.
    /// </summary>
    public string TimestampFormat { get; set; } = "HH:mm:ss ";

    /// <summary>
    /// Gets or sets a value indicating whether to use UTC timestamps. If false, local time is used.
    /// </summary>
    public bool UseUtcTimestamp { get; set; }

    /// <summary>
    /// Gets or sets the console to use for output. Defaults to SystemConsole.
    /// </summary>
    internal IConsole Console { get; set; } = sharedSystemConsole;
}
