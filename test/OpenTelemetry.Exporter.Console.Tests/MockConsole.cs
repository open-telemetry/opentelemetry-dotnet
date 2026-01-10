// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Console.Tests;

/// <summary>
/// Mock implementation of IConsole for testing.
/// </summary>
internal sealed class MockConsole : IConsole
{
    public const ConsoleColor DefaultForeground = ConsoleColor.Gray;
    public const ConsoleColor DefaultBackground = ConsoleColor.Black;

    private readonly List<string> output = new();
    private readonly List<(string Kind, ConsoleColor Color)> colorChanges = new();
    private ConsoleColor foregroundColor = DefaultForeground;
    private ConsoleColor backgroundColor = DefaultBackground;

    /// <summary>
    /// Gets the output that was written to the console.
    /// </summary>
    public IReadOnlyList<string> Output => this.output;

    /// <summary>
    /// Gets the color changes that were made.
    /// </summary>
    public IReadOnlyList<(string Kind, ConsoleColor Color)> ColorChanges => this.colorChanges;

    /// <inheritdoc/>
    public ConsoleColor ForegroundColor
    {
        get => this.foregroundColor;
        set
        {
            this.foregroundColor = value;
            this.colorChanges.Add(("Foreground", value));
        }
    }

    /// <inheritdoc/>
    public ConsoleColor BackgroundColor
    {
        get => this.backgroundColor;
        set
        {
            this.backgroundColor = value;
            this.colorChanges.Add(("Background", value));
        }
    }

    /// <inheritdoc/>
    public object SyncRoot { get; } = new();

    /// <inheritdoc/>
    public void ResetColor()
    {
        this.colorChanges.Add(("Reset", ConsoleColor.White));
    }

    /// <inheritdoc/>
    public void Write(string value)
    {
        this.output.Add(value);
    }

    /// <inheritdoc/>
    public void WriteLine(string value)
    {
        this.output.Add(value + Environment.NewLine);
    }

    /// <summary>
    /// Gets the combined output as a single string.
    /// </summary>
    /// <returns>The combined output string.</returns>
    public string GetOutput()
    {
        return string.Join(string.Empty, this.output);
    }
}
