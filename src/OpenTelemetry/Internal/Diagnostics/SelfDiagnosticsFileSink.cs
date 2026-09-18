// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Internal;

/// <summary>
/// Writes self-diagnostics entries to a rolling log file. This sink is purely the rolling-file
/// concern - naming, preamble/header emission, size-based rollover, retention pruning, and
/// failure recovery. Queueing and threading live in <see cref="SelfDiagnosticsSinkDispatcher"/>,
/// which invokes <see cref="Write"/> and <see cref="Flush"/> from a single pump thread.
/// </summary>
/// <remarks>
/// <para>
/// File naming: <c>otel-dotnet-{pid}-{processName}-{creation-timestamp}-{index}.log</c>.
/// The timestamp is fixed when the sink is created; the index increments on each rollover.
/// When a file reaches the size limit it is closed and a new file is opened. The oldest file
/// is pruned when the retention limit would be exceeded. Files are never truncated.
/// </para>
/// <para>
/// Each new file begins with a freshly generated preamble (see
/// <see cref="SelfDiagnosticsPreamble"/>) followed by the formatter's
/// <see cref="ISelfDiagnosticsFormatter.FileHeader"/>, so any single file grabbed for a support
/// bundle is self-contained and its environment snapshot is current for that file's window.
/// </para>
/// <para>
/// Writes are synchronous with <see cref="StreamWriter.AutoFlush"/> off; the dispatcher calls
/// <see cref="Flush"/> once per drained burst, turning N syscalls into one.
/// </para>
/// </remarks>
internal sealed class SelfDiagnosticsFileSink : ISelfDiagnosticsSink
{
    private static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromSeconds(30);

    // No BOM: log files are consumed by grep/tail-style tooling and concatenated into
    // support bundles, where a leading BOM is noise.
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);
    private static readonly int NewLineByteCount = Utf8NoBom.GetByteCount(Environment.NewLine);

    // Identifies the process, stable for its whole lifetime: pid, process name, and process
    // start time. The start time disambiguates a recycled pid, and holding it constant across
    // sink instances is what lets a recreated sink continue the same numbered file series
    // instead of inventing a parallel one.
    private static readonly string ProcessIdentity = BuildProcessIdentity();

    private readonly string logDirectory;
    private readonly long fileSizeLimitBytes;
    private readonly int maxRetainedFiles;
    private readonly TimeSpan retryInterval;

    // Oldest first. A List rather than a Queue so a failed delete can be left in place and
    // retried on the next rollover without disturbing the order.
    private readonly List<string> retainedFiles = [];
    private readonly Func<string>? preambleFactory;
    private readonly Action<string>? reportError;
    private readonly Func<DateTime> utcNow;

    // A file belonging to an outgoing sink that has not been disposed yet. Pruning it would
    // unlink a file still being written to.
    private string? excludeFromPruning;

    private StreamWriter writer = StreamWriter.Null;
    private long bytesWrittenToCurrentFile;
    private int fileIndex;
    private bool retentionSeeded;
    private DateTime nextOpenAttemptUtc = DateTime.MinValue;
    private bool failureReported;
    private bool startupMessageEmitted;
    private volatile bool disposed;

    internal SelfDiagnosticsFileSink(
        string logDirectory,
        int fileSizeLimitKilobytes,
        int maxRetainedFiles,
        Func<string>? preambleFactory,
        Action<string>? reportError = null,
        TimeSpan? retryInterval = null,
        string? excludeFromPruning = null,
        Func<DateTime>? utcNow = null)
    {
        this.logDirectory = logDirectory;
        this.preambleFactory = preambleFactory;
        this.reportError = reportError;
        this.retryInterval = retryInterval ?? DefaultRetryInterval;
        this.excludeFromPruning = excludeFromPruning;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);

        // FileSizeLimitKilobytes <= 0 disables size-based rollover (treated as unlimited).
        this.fileSizeLimitBytes = (long)Math.Max(0, fileSizeLimitKilobytes) * 1024;

        // Non-positive values disable automatic pruning.
        this.maxRetainedFiles = Math.Max(0, maxRetainedFiles);

        this.TryOpenWriter();
    }

    /// <inheritdoc/>
    public ISelfDiagnosticsFormatter? Formatter => SelfDiagnosticsTextFormatter.Instance;

    /// <summary>Gets a value indicating whether the log file is currently open for writing.</summary>
    internal bool IsActive => !ReferenceEquals(this.writer, StreamWriter.Null);

    /// <summary>Gets the path of the currently-open log file.</summary>
    internal string? CurrentFilePath { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns <see langword="true"/> even while broken: entries keep flowing so that the
    /// periodic reopen attempt in <see cref="Write"/> can recover the sink. Entries arriving
    /// while broken are dropped.
    /// </remarks>
    public bool IsEnabled(LogLevel level) => !this.disposed;

    /// <inheritdoc/>
    public void Write(in SelfDiagnosticsLogEntry entry, string? formatted)
    {
        if (formatted is null || this.disposed)
        {
            return;
        }

        try
        {
            if (!this.IsActive && !this.TryOpenWriter())
            {
                return; // broken and retry interval not yet elapsed - drop the entry
            }

            this.WriteLineAndTrack(formatted);

            if (this.fileSizeLimitBytes > 0
                && this.bytesWrittenToCurrentFile >= this.fileSizeLimitBytes)
            {
                this.RollOver();
            }
        }
        catch (Exception ex)
        {
            this.EnterBrokenState($"write to '{this.CurrentFilePath}' failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Flush()
    {
        if (!this.IsActive)
        {
            return;
        }

        try
        {
            this.writer.Flush();
        }
        catch (Exception ex)
        {
            this.EnterBrokenState($"flush of '{this.CurrentFilePath}' failed: {ex.Message}");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.disposed = true;

        // Guarded: `writer` is the shared StreamWriter.Null singleton whenever the sink is
        // broken or was never opened, and that instance is not ours to flush or dispose.
        if (!this.IsActive)
        {
            return;
        }

        try
        {
            this.writer.Flush();
        }
        catch
        {
            // Best-effort final flush.
        }

        try
        {
            this.writer.Dispose();
        }
        catch
        {
            // Ignore disposal errors.
        }

        this.writer = StreamWriter.Null;
    }

    /// <inheritdoc/>
    public void OnInstalled()
    {
        // The dispatcher invokes this only after it has disposed every removed sink. The
        // outgoing file therefore no longer needs its temporary handover protection and the
        // configured steady-state retention limit can be enforced immediately.
        this.excludeFromPruning = null;
        this.PruneOldFiles();
    }

    internal static string CreateProcessIdentity(int processId, string processName, DateTime startTimeUtc)
        => $"{processId}-{processName}-{startTimeUtc.ToString("yyyyMMdd-HHmmss.fffffff", CultureInfo.InvariantCulture)}";

    internal static string CreateFallbackProcessIdentity(Guid processInstanceId)
        => $"unknown-{processInstanceId:N}";

    private static string BuildProcessIdentity()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return CreateProcessIdentity(
                process.Id,
                process.ProcessName,
                process.StartTime.ToUniversalTime());
        }
        catch
        {
            // Process introspection is unavailable on some hosts. Fall back to a value captured
            // once per process so the file series is still stable across sink recreation, while
            // avoiding a collision with another process that encounters the same failure.
            return CreateFallbackProcessIdentity(Guid.NewGuid());
        }
    }

    /// <summary>
    /// Extracts the trailing numeric index from a log file path, or 0 when it does not parse.
    /// </summary>
    private static int ParseFileIndex(string path)
    {
        var name = Path.GetFileNameWithoutExtension(path);
        var separator = name.LastIndexOf('-');

        if (separator < 0 || separator >= name.Length - 1)
        {
            return 0;
        }

#if NET
        var suffix = name.AsSpan(separator + 1);
#else
        var suffix = name.Substring(separator + 1);
#endif

        return int.TryParse(suffix, NumberStyles.None, CultureInfo.InvariantCulture, out var index)
            ? index
            : 0;
    }

    private static bool IsSamePath(string path, string? other)
        => other is not null && string.Equals(path, other, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Attempts to open a new log file, honoring the retry interval while broken.
    /// Returns <see langword="true"/> when the writer is open on exit.
    /// </summary>
    private bool TryOpenWriter()
    {
        if (this.utcNow() < this.nextOpenAttemptUtc)
        {
            return false;
        }

        try
        {
            if (!Directory.Exists(this.logDirectory))
            {
                Directory.CreateDirectory(this.logDirectory);
            }

            if (!this.retentionSeeded)
            {
                var existingFiles = Directory.EnumerateFiles(
                    this.logDirectory,
                    $"otel-dotnet-{ProcessIdentity}-*.log");

                if (this.maxRetainedFiles == 0)
                {
                    // Unlimited retention needs only the highest index. Do not keep every
                    // historical path in memory when none of them will be pruned.
                    foreach (var existing in existingFiles)
                    {
                        this.fileIndex = Math.Max(this.fileIndex, ParseFileIndex(existing));
                    }
                }
                else
                {
                    // Seed bounded retention across sink recreations (e.g. hot-reload) and
                    // continue the index series from the highest file already on disk.
                    foreach (var existing in existingFiles.OrderBy(File.GetLastWriteTimeUtc))
                    {
                        this.retainedFiles.Add(existing);
                        this.fileIndex = Math.Max(this.fileIndex, ParseFileIndex(existing));
                    }
                }

                this.retentionSeeded = true;
            }

            this.fileIndex++;
            var fileName = $"otel-dotnet-{ProcessIdentity}-{this.fileIndex}.log";
            var filePath = Path.Combine(this.logDirectory, fileName);

            // FileShare.Read lets external tools tail the log while it's open.
            var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            var newWriter = new StreamWriter(stream, Utf8NoBom) { AutoFlush = false };

            this.CurrentFilePath = filePath;
            this.bytesWrittenToCurrentFile = stream.Length;
            this.writer = newWriter;
            this.failureReported = false;
            this.nextOpenAttemptUtc = DateTime.MinValue;

            if (this.maxRetainedFiles > 0)
            {
                this.retainedFiles.Add(filePath);
                this.PruneOldFiles();
            }

            this.WriteFilePrologue();

            if (!this.startupMessageEmitted)
            {
                this.startupMessageEmitted = true;
                try
                {
                    Console.Error.WriteLine($"OpenTelemetry SDK self-diagnostics: logging to {this.CurrentFilePath}");
                }
                catch
                {
                    // stderr may be unavailable or closed.
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            this.EnterBrokenState($"failed to open log file in '{this.logDirectory}': {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Writes the freshly generated preamble and the formatter's column header at the top of a
    /// newly opened file so every file is self-contained (support bundles often capture only one).
    /// </summary>
    private void WriteFilePrologue()
    {
        if (this.preambleFactory is not null)
        {
            string preamble;
            try
            {
                preamble = this.preambleFactory();
            }
            catch (Exception ex)
            {
                preamble = $"(preamble unavailable: {ex.Message})";
            }

            this.WriteLineAndTrack(preamble);
            this.WriteLineAndTrack(null);
        }

        var header = this.Formatter?.FileHeader;
        if (header is not null)
        {
            this.WriteLineAndTrack(header);
            this.WriteLineAndTrack(null);
        }

        this.writer.Flush();
    }

    private void WriteLineAndTrack(string? value)
    {
        this.writer.WriteLine(value);
        this.bytesWrittenToCurrentFile += NewLineByteCount;

        if (value is not null)
        {
            this.bytesWrittenToCurrentFile += Utf8NoBom.GetByteCount(value);
        }
    }

    /// <summary>
    /// Deletes the oldest files until the retention limit is satisfied.
    /// </summary>
    /// <remarks>
    /// Files that cannot be deleted, and files known to be open (this sink's current file, or the
    /// outgoing sink's file during a hot-reload swap) are left in the list and retried on the next
    /// rollover. Dropping them instead would leak them past retention forever on Windows, where
    /// the delete fails, and on Unix would unlink a file still being written to.
    /// </remarks>
    private void PruneOldFiles()
    {
        if (this.maxRetainedFiles == 0)
        {
            return;
        }

        var index = 0;

        while (this.retainedFiles.Count > this.maxRetainedFiles && index < this.retainedFiles.Count)
        {
            var oldest = this.retainedFiles[index];

            if (IsSamePath(oldest, this.CurrentFilePath) || IsSamePath(oldest, this.excludeFromPruning))
            {
                index++;
                continue;
            }

            try
            {
                if (File.Exists(oldest))
                {
                    File.Delete(oldest);
                }

                this.retainedFiles.RemoveAt(index);
            }
            catch
            {
                index++;
            }
        }
    }

    private void RollOver()
    {
        try
        {
            this.writer.Flush();
            this.writer.Dispose();
        }
        catch (Exception ex)
        {
            // Do not silently discard buffered content and open a replacement writer after a
            // failed flush. Use the same recovery policy as an ordinary write or flush failure.
            this.EnterBrokenState($"rollover of '{this.CurrentFilePath}' failed: {ex.Message}");
            return;
        }

        this.writer = StreamWriter.Null;

        // Open the replacement immediately; on failure the sink enters the broken state and
        // the next Write after the retry interval attempts recovery.
        this.TryOpenWriter();
    }

    /// <summary>
    /// Transitions to the broken state: closes the writer, schedules the next reopen attempt,
    /// and reports the failure once per outage (the flag resets on successful reopen).
    /// </summary>
    private void EnterBrokenState(string reason)
    {
        if (this.IsActive)
        {
            try
            {
                this.writer.Dispose();
            }
            catch
            {
                // Ignore disposal errors while entering the broken state.
            }

            this.writer = StreamWriter.Null;
        }

        this.nextOpenAttemptUtc = this.utcNow() + this.retryInterval;

        if (!this.failureReported)
        {
            this.failureReported = true;
            var message = $"OpenTelemetry SDK self-diagnostics file sink: {reason}. Entries will be dropped; retrying in {this.retryInterval.TotalSeconds:0}s.";
            if (this.reportError is not null)
            {
                this.reportError(message);
            }
            else
            {
                try
                {
                    Console.Error.WriteLine(message);
                }
                catch
                {
                    // Nowhere left to report.
                }
            }
        }
    }
}
