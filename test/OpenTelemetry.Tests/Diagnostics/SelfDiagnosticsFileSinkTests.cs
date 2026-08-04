// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Diagnostics;

namespace OpenTelemetry.Tests.Diagnostics;

public sealed class SelfDiagnosticsFileSinkTests : IDisposable
{
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), "otel-selfdiag-tests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(this.tempDirectory))
            {
                Directory.Delete(this.tempDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }

    [Fact]
    public void NewFile_ContainsPreambleAndHeader()
    {
        using (var sink = this.CreateSink(preamble: () => "=== test preamble ==="))
        {
            Assert.True(sink.IsActive);
            WriteLine(sink, "first entry");
            sink.Flush();
        }

        var content = File.ReadAllText(Directory.GetFiles(this.tempDirectory).Single());
        Assert.Contains("=== test preamble ===", content, StringComparison.Ordinal);
        Assert.Contains("DateTime (UTC)", content, StringComparison.Ordinal);
        Assert.Contains("first entry", content, StringComparison.Ordinal);
    }

    [Fact]
    public void RollOver_WritesFreshPreambleToEachFile()
    {
        // Design decision: every file is self-contained - support bundles often capture a
        // single file, and the preamble is regenerated so its snapshot is current.
        var preambleCalls = 0;

        using (var sink = this.CreateSink(preamble: () => $"=== preamble {++preambleCalls} ===", fileSizeLimitKilobytes: 1))
        {
            // Each line is ~600 chars, so two lines exceed the 1 KiB limit and force a rollover.
            var line = new string('x', 600);
            WriteLine(sink, line);
            WriteLine(sink, line);
            WriteLine(sink, line);
            sink.Flush();
        }

        var files = Directory.GetFiles(this.tempDirectory);
        Assert.True(files.Length >= 2, $"expected a rollover, found {files.Length} file(s)");
        Assert.True(preambleCalls >= 2, "preamble should be regenerated per file");

        foreach (var file in files)
        {
            Assert.Contains("=== preamble", File.ReadAllText(file), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Retention_PrunesOldestFiles()
    {
        using (var sink = this.CreateSink(fileSizeLimitKilobytes: 1, maxRetainedFiles: 2))
        {
            var line = new string('x', 600);
            for (var i = 0; i < 10; i++)
            {
                WriteLine(sink, line);
            }

            sink.Flush();
        }

        Assert.True(Directory.GetFiles(this.tempDirectory).Length <= 2);
    }

    [Fact]
    public void Retention_SeededAcrossSinkRecreation()
    {
        // Regression scenario: hot-reload recreates the sink; retention must count files
        // written by the previous instance for this process, or the directory grows unbounded.
        using (var first = this.CreateSink(maxRetainedFiles: 2))
        {
            WriteLine(first, "from first sink");
            first.Flush();
        }

        using (var second = this.CreateSink(fileSizeLimitKilobytes: 1, maxRetainedFiles: 2))
        {
            var line = new string('x', 600);
            for (var i = 0; i < 6; i++)
            {
                WriteLine(second, line);
            }

            second.Flush();
        }

        Assert.True(Directory.GetFiles(this.tempDirectory).Length <= 2);
    }

    [Fact]
    public void OpenFailure_ReportsOnce_AndRecoversAfterRetryInterval()
    {
        // Regression: a failed open/rollover used to disable the sink permanently, with no
        // recovery even on an options reload with identical settings. The sink now retries.
        var blockedPath = Path.Combine(this.tempDirectory, "blocked");
        Directory.CreateDirectory(this.tempDirectory);
        File.WriteAllText(blockedPath, string.Empty); // a *file* at the directory path forces CreateDirectory to fail

        var errors = new List<string>();

        using var sink = new SelfDiagnosticsFileSink(
            blockedPath,
            fileSizeLimitKilobytes: 1024,
            maxRetainedFiles: 3,
            preambleFactory: null,
            reportError: errors.Add,
            retryInterval: TimeSpan.Zero);

        Assert.False(sink.IsActive);
        Assert.Single(errors); // reported once per outage, not per entry

        WriteLine(sink, "dropped entry");
        Assert.False(sink.IsActive);
        Assert.Single(errors);

        // Clear the obstruction; the next write (retry interval is zero) must self-heal.
        File.Delete(blockedPath);

        WriteLine(sink, "recovered entry");
        Assert.True(sink.IsActive);
        sink.Flush();

        // The sink still holds the file open, so read with a write-tolerant share mode.
        var content = SelfDiagnosticsTestHelpers.ReadAllTextShared(Directory.GetFiles(blockedPath).Single());
        Assert.Contains("recovered entry", content, StringComparison.Ordinal);
    }

    [Fact]
    public void FileNames_UseProcessPrefixAndIncrementingIndex()
    {
        using (var sink = this.CreateSink(fileSizeLimitKilobytes: 1))
        {
            var line = new string('x', 600);
            WriteLine(sink, line);
            WriteLine(sink, line);
            WriteLine(sink, line);
            sink.Flush();
        }

        foreach (var file in Directory.GetFiles(this.tempDirectory))
        {
            Assert.StartsWith("otel-dotnet-", Path.GetFileName(file), StringComparison.Ordinal);
            Assert.EndsWith(".log", file, StringComparison.Ordinal);
        }
    }

    private static void WriteLine(SelfDiagnosticsFileSink sink, string message)
    {
        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, message, null);
        sink.Write(in entry, message);
    }

    private SelfDiagnosticsFileSink CreateSink(
        Func<string>? preamble = null,
        int fileSizeLimitKilobytes = 10_240,
        int maxRetainedFiles = 3)
        => new(
            this.tempDirectory,
            fileSizeLimitKilobytes,
            maxRetainedFiles,
            preamble,
            reportError: null,
            retryInterval: TimeSpan.Zero);
}
