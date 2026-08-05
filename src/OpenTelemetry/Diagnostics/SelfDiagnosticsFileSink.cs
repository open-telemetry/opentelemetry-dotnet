// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace OpenTelemetry.Diagnostics;

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

    // Stable for the process lifetime; shared prefix used to match existing log files
    // from prior sink instances when seeding the retention queue on reconstruction.
    private readonly string processPrefix;

    // Per-instance: captures timestamp at sink creation, not process start, so
    // filenames don't collide when the sink is torn down and recreated via hot-reload.
    private readonly string fileNameSuffix;

    private readonly string logDirectory;
    private readonly long fileSizeLimitBytes;
    private readonly int maxRetainedFiles;
    private readonly TimeSpan retryInterval;
    private readonly Queue<string> retainedFiles = new();
    private readonly Func<string>? preambleFactory;
    private readonly Action<string>? reportError;

    private StreamWriter writer = StreamWriter.Null;
    private long bytesWrittenToCurrentFile;
    private int fileIndex;
    private bool retentionSeeded;
    private DateTime nextOpenAttemptUtc = DateTime.MinValue;
    private bool failureReported;
    private volatile bool disposed;

    internal SelfDiagnosticsFileSink(
        string logDirectory,
        int fileSizeLimitKilobytes,
        int maxRetainedFiles,
        Func<string>? preambleFactory,
        Action<string>? reportError = null,
        TimeSpan? retryInterval = null)
    {
        this.processPrefix = BuildProcessPrefix();
        this.fileNameSuffix = BuildFileNameSuffix(this.processPrefix);
        this.logDirectory = logDirectory;
        this.preambleFactory = preambleFactory;
        this.reportError = reportError;
        this.retryInterval = retryInterval ?? DefaultRetryInterval;

        // FileSizeLimitKilobytes <= 0 disables size-based rollover (treated as unlimited).
        this.fileSizeLimitBytes = (long)Math.Max(0, fileSizeLimitKilobytes) * 1024;

        // MaxRetainedFiles <= 0 is clamped to 1 (always retain at least the current file).
        this.maxRetainedFiles = Math.Max(1, maxRetainedFiles);

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

            this.writer.WriteLine(formatted);

            // Track approximate file size and roll over when the limit is reached.
            // The char count is used as a proxy for byte count (UTF-8 multi-byte chars
            // are uncommon in diagnostic messages and the limit is a soft boundary).
            this.bytesWrittenToCurrentFile += formatted.Length + Environment.NewLine.Length;

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

    private static string BuildProcessPrefix()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            return $"{process.Id}-{process.ProcessName}";
        }
        catch
        {
            return "unknown";
        }
    }

    private static string BuildFileNameSuffix(string processPrefix)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmssfffZ", CultureInfo.InvariantCulture);
        return $"{processPrefix}-{timestamp}-";
    }

    /// <summary>
    /// Attempts to open a new log file, honoring the retry interval while broken.
    /// Returns <see langword="true"/> when the writer is open on exit.
    /// </summary>
    private bool TryOpenWriter()
    {
        if (DateTime.UtcNow < this.nextOpenAttemptUtc)
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
                // Seed the retention queue with any existing log files written by this process
                // so that MaxRetainedFiles is enforced across sink recreations (e.g. hot-reload).
                // Sorting by last-write time ensures the oldest files are pruned first.
                foreach (var existing in Directory.GetFiles(this.logDirectory, $"otel-dotnet-{this.processPrefix}-*.log")
                    .OrderBy(File.GetLastWriteTimeUtc))
                {
                    this.retainedFiles.Enqueue(existing);
                }

                this.retentionSeeded = true;
            }

            this.fileIndex++;
            var fileName = $"otel-dotnet-{this.fileNameSuffix}{this.fileIndex}.log";
            var filePath = Path.Combine(this.logDirectory, fileName);

            // FileShare.Read lets external tools tail the log while it's open.
            var stream = new FileStream(filePath, FileMode.Append, FileAccess.Write, FileShare.Read);
            var newWriter = new StreamWriter(stream, Utf8NoBom) { AutoFlush = false };

            this.CurrentFilePath = filePath;
            this.bytesWrittenToCurrentFile = 0;
            this.writer = newWriter;
            this.failureReported = false;
            this.nextOpenAttemptUtc = DateTime.MinValue;

            this.retainedFiles.Enqueue(filePath);
            this.PruneOldFiles();

            this.WriteFilePrologue();

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

            this.writer.WriteLine(preamble);
            this.writer.WriteLine();
        }

        var header = this.Formatter?.FileHeader;
        if (header is not null)
        {
            this.writer.WriteLine(header);
            this.writer.WriteLine();
        }

        this.writer.Flush();
    }

    private void PruneOldFiles()
    {
        while (this.retainedFiles.Count > this.maxRetainedFiles)
        {
            var oldest = this.retainedFiles.Dequeue();
            try
            {
                if (File.Exists(oldest))
                {
                    File.Delete(oldest);
                }
            }
            catch
            {
                // Best-effort: if delete fails (e.g. file in use), skip it.
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
        catch
        {
            // Ignore disposal errors during rollover.
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
        try
        {
            this.writer.Dispose();
        }
        catch
        {
            // Ignore disposal errors while entering the broken state.
        }

        this.writer = StreamWriter.Null;
        this.nextOpenAttemptUtc = DateTime.UtcNow + this.retryInterval;

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
