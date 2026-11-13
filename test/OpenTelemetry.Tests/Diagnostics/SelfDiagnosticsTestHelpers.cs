// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests.Diagnostics;

internal static class SelfDiagnosticsTestHelpers
{
    /// <summary>
    /// Polls <paramref name="condition"/> until it returns true or the timeout elapses.
    /// The dispatcher pump is asynchronous, so tests observing sink output must wait.
    /// </summary>
    public static bool WaitUntil(Func<bool> condition, int timeoutMilliseconds = 5000)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        while (stopwatch.ElapsedMilliseconds < timeoutMilliseconds)
        {
            if (condition())
            {
                return true;
            }

            Thread.Sleep(10);
        }

        return condition();
    }

    /// <summary>
    /// Reads a file that may still be held open for writing by a live file sink
    /// (<see cref="File.ReadAllText(string)"/> would hit a sharing violation).
    /// </summary>
    public static string ReadAllTextShared(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
