// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Formatting.Compact;

/// <summary>
/// Interface for console operations to support color output and testing.
/// </summary>
internal interface IConsole
{
    /// <summary>
    /// Gets or sets the foreground color.
    /// </summary>
    ConsoleColor ForegroundColor { get; set; }

    /// <summary>
    /// Gets or sets the background color.
    /// </summary>
    ConsoleColor BackgroundColor { get; set; }

    /// <summary>
    /// Resets the console colors to their defaults.
    /// </summary>
    void ResetColor();

    /// <summary>
    /// Writes the specified string value to the console.
    /// </summary>
    /// <param name="value">The string to write.</param>
    void Write(string value);

    /// <summary>
    /// Writes the specified string value followed by a line terminator to the console.
    /// </summary>
    /// <param name="value">The string to write.</param>
    void WriteLine(string value);
}
