// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

/// <summary>
/// Implementation of IConsole that wraps System.Console.
/// </summary>
internal sealed class SystemConsole : IConsole
{
    /// <inheritdoc/>
    public ConsoleColor ForegroundColor
    {
        get => Console.ForegroundColor;
        set => Console.ForegroundColor = value;
    }

    /// <inheritdoc/>
    public ConsoleColor BackgroundColor
    {
        get => Console.BackgroundColor;
        set => Console.BackgroundColor = value;
    }

    /// <inheritdoc/>
    public object SyncRoot { get; } = new();

    /// <inheritdoc/>
    public void ResetColor() => Console.ResetColor();

    /// <inheritdoc/>
    public void Write(string value) => Console.Write(value);

    /// <inheritdoc/>
    public void WriteLine(string value) => Console.WriteLine(value);
}
