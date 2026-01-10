// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Formatting.Compact;

/// <summary>
/// Implementation of IConsole that wraps System.Console.
/// </summary>
internal sealed class SystemConsole : IConsole
{
    /// <inheritdoc/>
    public ConsoleColor ForegroundColor
    {
        get => System.Console.ForegroundColor;
        set => System.Console.ForegroundColor = value;
    }

    /// <inheritdoc/>
    public ConsoleColor BackgroundColor
    {
        get => System.Console.BackgroundColor;
        set => System.Console.BackgroundColor = value;
    }

    /// <inheritdoc/>
    public void ResetColor() => System.Console.ResetColor();

    /// <inheritdoc/>
    public void Write(string value) => System.Console.Write(value);

    /// <inheritdoc/>
    public void WriteLine(string value) => System.Console.WriteLine(value);
}
