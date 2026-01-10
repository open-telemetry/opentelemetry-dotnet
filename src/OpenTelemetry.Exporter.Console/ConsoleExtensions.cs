// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter;

internal static class ConsoleExtensions
{
    public static IConsole WriteColor(this IConsole console, string value, ConsoleColor foreground, ConsoleColor background)
    {
        var originalForeground = console.ForegroundColor;
        var originalBackground = console.BackgroundColor;
        console.ForegroundColor = foreground;
        console.BackgroundColor = background;
        console.Write(value);
        console.ForegroundColor = originalForeground;
        console.BackgroundColor = originalBackground;
        return console;
    }
}
