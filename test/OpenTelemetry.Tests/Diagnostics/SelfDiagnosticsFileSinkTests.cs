// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;

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
    public void FirstOpen_WritesStartupMessageToStderr()
    {
        using var captured = new StringWriter();
        var previous = Console.Error;
        Console.SetError(captured);

        string? expectedPath;

        try
        {
            using var sink = this.CreateSink();
            expectedPath = sink.CurrentFilePath;
        }
        finally
        {
            Console.SetError(previous);
        }

        var output = captured.ToString();
        Assert.NotNull(expectedPath);
        Assert.Contains("OpenTelemetry SDK self-diagnostics: logging to", output, StringComparison.Ordinal);
        Assert.Contains(expectedPath, output, StringComparison.Ordinal);
    }

    [Fact]
    public void RollOver_DoesNotRepeatStartupMessage()
    {
        using var captured = new StringWriter();
        var previous = Console.Error;
        Console.SetError(captured);

        try
        {
            using var sink = this.CreateSink(fileSizeLimitKilobytes: 1);
            var line = new string('x', 600);
            WriteLine(sink, line);
            WriteLine(sink, line);
            WriteLine(sink, line);
            sink.Flush();
        }
        finally
        {
            Console.SetError(previous);
        }

        var output = captured.ToString();
        const string marker = "OpenTelemetry SDK self-diagnostics: logging to";
        var count = 0;
        var pos = 0;
        while ((pos = output.IndexOf(marker, pos, StringComparison.Ordinal)) >= 0)
        {
            count++;
            pos += marker.Length;
        }

        Assert.Equal(1, count);
    }

    [Fact]
    public void FileSizeLimit_CountsPreambleAndHeaderBytes()
    {
        using (var sink = this.CreateSink(
            preamble: () => new string('p', 2_048),
            fileSizeLimitKilobytes: 1))
        {
            WriteLine(sink, "the entry that crosses the boundary");
            sink.Flush();
        }

        var files = Directory.GetFiles(this.tempDirectory, "*.log");
        Assert.True(files.Length >= 2, $"expected a rollover, found {files.Length} file(s)");
        Assert.True(
            files.Any(file => new FileInfo(file).Length >= 1_024),
            "no file reached the configured byte boundary");
    }

    [Fact]
    public void FileSizeLimit_CountsUtf8BytesRatherThanUtf16Characters()
    {
        using (var sink = this.CreateSink(fileSizeLimitKilobytes: 1))
        {
            WriteLine(sink, new string('\u20AC', 400));
            sink.Flush();
        }

        var files = Directory.GetFiles(this.tempDirectory, "*.log");
        Assert.True(files.Length >= 2, $"expected a rollover, found {files.Length} file(s)");
        Assert.True(
            files.Any(file => new FileInfo(file).Length >= 1_024),
            "no file reached the configured byte boundary");
    }

    [Fact]
    public void PreambleFactoryThrows_FileStillOpensWithPlaceholder()
    {
        var errors = new List<string>();

        using (var sink = new SelfDiagnosticsFileSink(
            this.tempDirectory,
            fileSizeLimitKilobytes: 10_240,
            maxRetainedFiles: 3,
            preambleFactory: static () => throw new InvalidOperationException("preamble boom"),
            reportError: errors.Add,
            retryInterval: TimeSpan.Zero))
        {
            Assert.True(sink.IsActive);
            WriteLine(sink, "entry after a broken preamble");
            sink.Flush();
        }

        Assert.Empty(errors); // a broken preamble is not a sink failure

        var content = File.ReadAllText(Directory.GetFiles(this.tempDirectory).Single());
        Assert.Contains("(preamble unavailable: preamble boom)", content, StringComparison.Ordinal);
        Assert.Contains("DateTime (UTC)", content, StringComparison.Ordinal);
        Assert.Contains("entry after a broken preamble", content, StringComparison.Ordinal);
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

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void NonPositiveMaxRetainedFiles_DisablesPruningWithoutTrackingHistoricalPaths(int maxRetainedFiles)
    {
        HashSet<string> filesBeforeRecreation;

        using (var sink = this.CreateSink(fileSizeLimitKilobytes: 1, maxRetainedFiles: maxRetainedFiles))
        {
            var line = new string('x', 600);
            for (var i = 0; i < 10; i++)
            {
                WriteLine(sink, line);
            }

            sink.Flush();
            Assert.True(sink.IsActive);
            Assert.Empty(GetRetainedFiles(sink));

            filesBeforeRecreation = [.. Directory.GetFiles(this.tempDirectory)];
            Assert.True(filesBeforeRecreation.Count > 1);
        }

        using (var sink = this.CreateSink(fileSizeLimitKilobytes: 1, maxRetainedFiles: maxRetainedFiles))
        {
            WriteLine(sink, new string('y', 1_100));
            sink.Flush();

            Assert.True(sink.IsActive);
            Assert.Empty(GetRetainedFiles(sink));
        }

        Assert.All(filesBeforeRecreation, file => Assert.True(File.Exists(file)));
        Assert.True(Directory.GetFiles(this.tempDirectory).Length > filesBeforeRecreation.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NonPositiveFileSizeLimit_DisablesRollOver(int fileSizeLimitKilobytes)
    {
        using (var sink = this.CreateSink(fileSizeLimitKilobytes: fileSizeLimitKilobytes))
        {
            var line = new string('x', 1024);
            for (var i = 0; i < 32; i++)
            {
                WriteLine(sink, line);
            }

            sink.Flush();
        }

        Assert.Single(Directory.GetFiles(this.tempDirectory));
    }

    [Fact]
    public void OpenFailure_ReportsOnce_AndRecoversAfterRetryInterval()
    {
        var blockedPath = Path.Combine(this.tempDirectory, "blocked");
        Directory.CreateDirectory(this.tempDirectory);
        File.WriteAllText(blockedPath, string.Empty); // a file at the directory path forces CreateDirectory to fail

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
    public void WriteFailure_ReportsOnce_AndDropsEntriesUntilRetryIntervalElapses()
    {
        var errors = new List<string>();

        using var sink = new SelfDiagnosticsFileSink(
            this.tempDirectory,
            fileSizeLimitKilobytes: 10_240,
            maxRetainedFiles: 3,
            preambleFactory: null,
            reportError: errors.Add,
            retryInterval: TimeSpan.FromHours(1));

        Assert.True(sink.IsActive);
        WriteLine(sink, "before the failure");

        var filePath = sink.CurrentFilePath;
        Assert.NotNull(filePath);

        CloseUnderlyingWriter(sink);

        WriteLine(sink, "the entry that fails");

        Assert.False(sink.IsActive);
        Assert.Single(errors);
        Assert.Contains("write to", errors[0], StringComparison.Ordinal);
        Assert.Contains("Entries will be dropped", errors[0], StringComparison.Ordinal);

        // The retry interval has not elapsed, so further entries are dropped without a reopen
        // attempt: no second report and no second file.
        WriteLine(sink, "dropped entry one");
        WriteLine(sink, "dropped entry two");

        Assert.False(sink.IsActive);
        Assert.Single(errors);
        Assert.Single(Directory.GetFiles(this.tempDirectory));

        var content = File.ReadAllText(filePath);
        Assert.Contains("before the failure", content, StringComparison.Ordinal);
        Assert.DoesNotContain("dropped entry", content, StringComparison.Ordinal);
    }

    [Fact]
    public void FlushFailure_EntersBrokenStateAndReportsOnce()
    {
        var errors = new List<string>();

        using var sink = new SelfDiagnosticsFileSink(
            this.tempDirectory,
            fileSizeLimitKilobytes: 10_240,
            maxRetainedFiles: 3,
            preambleFactory: null,
            reportError: errors.Add,
            retryInterval: TimeSpan.FromHours(1));

        Assert.True(sink.IsActive);
        WriteLine(sink, "an entry to flush");

        CloseUnderlyingWriter(sink);

        sink.Flush();

        Assert.False(sink.IsActive);
        Assert.Single(errors);
        Assert.Contains("flush of", errors[0], StringComparison.Ordinal);

        // A broken sink stays quiet: Flush short-circuits on the inactive writer instead of
        // reporting the same outage again.
        sink.Flush();
        Assert.Single(errors);
    }

    [Fact]
    public void RollOverFlushFailure_EntersBrokenStateAndReportsOnce()
    {
        var errors = new List<string>();

        using var sink = new SelfDiagnosticsFileSink(
            this.tempDirectory,
            fileSizeLimitKilobytes: 1,
            maxRetainedFiles: 3,
            preambleFactory: null,
            reportError: errors.Add,
            retryInterval: TimeSpan.FromHours(1));

        Assert.True(sink.IsActive);
        using var replacement = new StreamWriter(new FlushThrowingStream());
        ReplaceUnderlyingWriter(sink, replacement);

        WriteLine(sink, new string('x', 1_024));

        Assert.False(sink.IsActive);
        Assert.Single(errors);
        Assert.Contains("rollover of", errors[0], StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(this.tempDirectory));
    }

    [Fact]
    public void ProcessIdentity_UsesFullStartTimePrecision()
    {
        var processName = "test-process";
        var startTime = new DateTime(2026, 8, 10, 14, 43, 0, DateTimeKind.Utc);

        var first = SelfDiagnosticsFileSink.CreateProcessIdentity(123, processName, startTime);
        var second = SelfDiagnosticsFileSink.CreateProcessIdentity(123, processName, startTime.AddTicks(1));

        Assert.NotEqual(first, second);
        Assert.Equal("123-test-process-20260810-144300.0000000", first);
    }

    [Fact]
    public void FallbackProcessIdentity_UsesPerProcessIdentifier()
    {
        var first = SelfDiagnosticsFileSink.CreateFallbackProcessIdentity(
            new Guid("00000000-0000-0000-0000-000000000001"));
        var second = SelfDiagnosticsFileSink.CreateFallbackProcessIdentity(
            new Guid("00000000-0000-0000-0000-000000000002"));

        Assert.NotEqual(first, second);
        Assert.Equal("unknown-00000000000000000000000000000001", first);
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

    [Fact]
    public void Recreation_ProtectsTheOutgoingFileOnlyUntilInstallationCompletes()
    {
        // A recreated sink must continue the index series without reopening the outgoing file.
        using var first = this.CreateSink();
        WriteLine(first, "from the outgoing sink");
        var firstPath = first.CurrentFilePath;

        Assert.NotNull(firstPath);
        Assert.EndsWith("-1.log", firstPath, StringComparison.Ordinal);

        using var second = this.CreateSink(maxRetainedFiles: 1, excludeFromPruning: firstPath);
        WriteLine(second, "from the incoming sink");
        second.Flush();

        var secondPath = second.CurrentFilePath;
        Assert.NotNull(secondPath);
        Assert.EndsWith("-2.log", secondPath, StringComparison.Ordinal);
        Assert.True(File.Exists(firstPath), "the active outgoing file was pruned during handover");

        first.Dispose();
        second.OnInstalled();

        Assert.False(File.Exists(firstPath), "the closed outgoing file remained excluded from retention");
        Assert.True(File.Exists(secondPath), "the current file was pruned");
        Assert.Single(Directory.GetFiles(this.tempDirectory, "*.log"));
    }

    [Fact]
    public void Dispose_FlushesBufferedContentAndStopsAcceptingEntries()
    {
        string? filePath;

        using (var sink = this.CreateSink())
        {
            WriteLine(sink, "buffered until disposal");
            filePath = sink.CurrentFilePath;

            // Deliberately no Flush() call: disposal is the only thing that can get this on disk.
            sink.Dispose();

            Assert.False(sink.IsActive);
            Assert.False(sink.IsEnabled(LogLevel.Error));

            WriteLine(sink, "after disposal");
        }

        Assert.NotNull(filePath);

        var content = File.ReadAllText(filePath);
        Assert.Contains("buffered until disposal", content, StringComparison.Ordinal);
        Assert.DoesNotContain("after disposal", content, StringComparison.Ordinal);
        Assert.Single(Directory.GetFiles(this.tempDirectory));
    }

    private static void WriteLine(SelfDiagnosticsFileSink sink, string message)
    {
        var entry = SelfDiagnosticsLogEntry.Capture(LogLevel.Warning, default, message, null);
        sink.Write(in entry, message);
    }

    private static void CloseUnderlyingWriter(SelfDiagnosticsFileSink sink)
    {
        var field = typeof(SelfDiagnosticsFileSink).GetField(
            "writer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        var writer = field.GetValue(sink) as StreamWriter;
        Assert.NotNull(writer);
        writer.Dispose();
    }

    private static void ReplaceUnderlyingWriter(SelfDiagnosticsFileSink sink, StreamWriter replacement)
    {
        var field = typeof(SelfDiagnosticsFileSink).GetField(
            "writer",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);

        var writer = field.GetValue(sink) as StreamWriter;
        Assert.NotNull(writer);
        writer.Dispose();

        field.SetValue(sink, replacement);
    }

    private static List<string> GetRetainedFiles(SelfDiagnosticsFileSink sink)
    {
        var field = typeof(SelfDiagnosticsFileSink).GetField(
            "retainedFiles",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(field);
        return Assert.IsType<List<string>>(field.GetValue(sink));
    }

    private SelfDiagnosticsFileSink CreateSink(
        Func<string>? preamble = null,
        int fileSizeLimitKilobytes = 10_240,
        int maxRetainedFiles = 3,
        string? excludeFromPruning = null)
        => new(
            this.tempDirectory,
            fileSizeLimitKilobytes,
            maxRetainedFiles,
            preamble,
            reportError: null,
            retryInterval: TimeSpan.Zero,
            excludeFromPruning: excludeFromPruning);

    private sealed class FlushThrowingStream : Stream
    {
        private readonly MemoryStream inner = new();

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => true;

        public override long Length => this.inner.Length;

        public override long Position
        {
            get => this.inner.Position;
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new IOException("flush boom");

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => this.inner.Write(buffer, offset, count);
    }
}
