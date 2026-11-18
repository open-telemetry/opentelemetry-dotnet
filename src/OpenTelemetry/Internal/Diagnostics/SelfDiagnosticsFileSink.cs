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
#if NET
[System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
#endif
internal sealed class SelfDiagnosticsFileSink : ISelfDiagnosticsSink
{
#if NET
    // The log carries the environment snapshot (exporter credentials under AllValues) and
    // exception detail, so on Unix the directory and files are created owner-only rather than
    // inheriting whatever the process umask leaves (typically 0755/0644). Existing directories
    // are left as they are.
    private const UnixFileMode OwnerOnlyDirectoryMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode OwnerOnlyFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
#endif

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

    // Names of the files that sinks in this process created under bounded retention and have
    // not pruned yet, by the directory they are in. The log directory may be shared with other
    // users, so a file whose name matches the series is not necessarily one of ours: a
    // recreated sink only takes over (and later deletes) files recorded here. Names are
    // compared exactly: the file system returns the names of our files as we created them, so a
    // name that differs from one of them only in case belongs to another file, which it is on
    // a case-sensitive file system. Directories are compared as configured (see
    // GetDirectoryKey), so a sink recreated for the same directory under another spelling
    // leaves the earlier files alone rather than taking them over.
    private static readonly Dictionary<string, HashSet<string>> CreatedFiles = new(StringComparer.Ordinal);
    private static readonly Lock CreatedFilesLock = new();

    private readonly string logDirectory;
    private readonly string directoryKey;
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
    private bool announceLogFile;
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
        Func<DateTime>? utcNow = null,
        bool announceLogFile = true)
    {
        this.logDirectory = logDirectory;
        this.directoryKey = GetDirectoryKey(logDirectory);
        this.preambleFactory = preambleFactory;
        this.reportError = reportError;
        this.retryInterval = retryInterval ?? DefaultRetryInterval;
        this.excludeFromPruning = excludeFromPruning;
        this.utcNow = utcNow ?? (() => DateTime.UtcNow);
        this.announceLogFile = announceLogFile;

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

    internal static bool WasCreatedByThisProcess(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var directoryKey = GetDirectoryKey(Path.GetDirectoryName(fullPath) ?? string.Empty);

        lock (CreatedFilesLock)
        {
            return CreatedFiles.TryGetValue(directoryKey, out var names)
                && names.Contains(Path.GetFileName(fullPath));
        }
    }

    internal void UpdateAnnouncement(bool announceLogFile)
        => this.announceLogFile = announceLogFile;

    private static string BuildProcessIdentity()
    {
        try
        {
#if NET
            if (OperatingSystem.IsIOS())
            {
                // Process.StartTime is unsupported on iOS.
                return CreateFallbackProcessIdentity(Guid.NewGuid());
            }
#endif

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

#if NET11_0_OR_GREATER
        var separator = name.LastIndexOf('-', StringComparison.Ordinal);
#else
        var separator = name.LastIndexOf('-');
#endif

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

    // The registry is keyed by the directory as the sink was configured with it, made absolute
    // and stripped of a trailing separator, so that the spellings a configuration reload is
    // likely to vary between agree. Resolving links or letter case would need file system
    // specific calls; a key that does not match only leaves the earlier files unpruned.
    private static string GetDirectoryKey(string directory)
    {
        try
        {
            directory = Path.GetFullPath(directory);
        }
        catch (Exception)
        {
            // An unusable path fails, and is reported, when the directory is created.
        }

        return directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    // Something other than the sink that created a file can remove it from the log directory.
    // The names recorded for a directory are reconciled with a listing of it when a sink seeds
    // its retention there, so that a process whose logs keep being removed between sink
    // recreations does not accumulate the name of every log it has ever written. Only that
    // directory is touched, and only a listing that succeeded is trusted, so a file that cannot
    // be checked at the time stays recorded. Returns the recorded names that are present.
    private static HashSet<string> ReconcileCreatedFiles(string directoryKey, HashSet<string> existingNames)
    {
        lock (CreatedFilesLock)
        {
            if (!CreatedFiles.TryGetValue(directoryKey, out var names))
            {
                return [];
            }

            names.IntersectWith(existingNames);

            if (names.Count == 0)
            {
                CreatedFiles.Remove(directoryKey);
                return [];
            }

            return new HashSet<string>(names, names.Comparer);
        }
    }

    private static void RecordCreatedFile(string directoryKey, string fileName)
    {
        lock (CreatedFilesLock)
        {
            if (!CreatedFiles.TryGetValue(directoryKey, out var names))
            {
                names = new HashSet<string>(StringComparer.Ordinal);
                CreatedFiles.Add(directoryKey, names);
            }

            names.Add(fileName);
        }
    }

    private static void ForgetCreatedFile(string directoryKey, string fileName)
    {
        lock (CreatedFilesLock)
        {
            if (CreatedFiles.TryGetValue(directoryKey, out var names)
                && names.Remove(fileName)
                && names.Count == 0)
            {
                CreatedFiles.Remove(directoryKey);
            }
        }
    }

    // The modification time of a file that something else replaced can be unreadable (for
    // example outside the range of DateTime). Such a file sorts as the oldest.
    private static DateTime GetLastWriteTimeUtcOrMinValue(string path)
    {
        try
        {
            return File.GetLastWriteTimeUtc(path);
        }
        catch (Exception)
        {
            return DateTime.MinValue;
        }
    }

    // An index that cannot be incremented is not one this process wrote.
    private static int ParseSeedIndex(string path)
    {
        var index = ParseFileIndex(path);
        return index == int.MaxValue ? 0 : index;
    }

    private static void CreateLogDirectory(string path)
    {
#if NET
        if (!OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path, OwnerOnlyDirectoryMode);
            return;
        }
#endif

        Directory.CreateDirectory(path);
    }

    private static FileStream OpenLogFile(string path)
    {
        // The index is always past any file already in the series, so the name is expected to
        // be free; an existing entry is most likely a symbolic link planted by another user of
        // the log directory. FileMode.CreateNew fails instead of following it (and writing into
        // its target), and the caller then tries again with a different name.
        // FileShare.Read lets external tools tail the log while it's open.
#if NET
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.Read,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = OwnerOnlyFileMode;
        }

        return new FileStream(path, options);
#else
        return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
#endif
    }

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
                CreateLogDirectory(this.logDirectory);
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
                        this.fileIndex = Math.Max(this.fileIndex, ParseSeedIndex(existing));
                    }
                }
                else
                {
                    // Seed bounded retention across sink recreations (e.g. hot-reload). Every
                    // matching name moves the index series on so that it is stepped over, but
                    // only the files that this process created are retained: anything else in
                    // the directory is not ours to count or to delete.
                    var existingPaths = new List<string>();
                    var existingNames = new HashSet<string>(StringComparer.Ordinal);

                    foreach (var existing in existingFiles)
                    {
                        this.fileIndex = Math.Max(this.fileIndex, ParseSeedIndex(existing));
                        existingPaths.Add(existing);
                        existingNames.Add(Path.GetFileName(existing));
                    }

                    var createdNames = ReconcileCreatedFiles(this.directoryKey, existingNames);

                    this.retainedFiles.AddRange(
                        existingPaths
                            .Where(path => createdNames.Contains(Path.GetFileName(path)))
                            .OrderBy(GetLastWriteTimeUtcOrMinValue));
                }

                this.retentionSeeded = true;
            }

            this.fileIndex++;
            var fileName = $"otel-dotnet-{ProcessIdentity}-{this.fileIndex}.log";
            var filePath = Path.Combine(this.logDirectory, fileName);

            FileStream stream;

            try
            {
                stream = OpenLogFile(filePath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The name is already in use (or otherwise cannot be created). Names of the
                // form above are predictable, so rather than waiting out the retry interval
                // and then trying the next predictable name, try once straight away with a
                // name that cannot be known in advance. A further failure is handled below.
                filePath = Path.Combine(
                    this.logDirectory,
                    $"otel-dotnet-{ProcessIdentity}-{Guid.NewGuid():N}-{this.fileIndex}.log");

                stream = OpenLogFile(filePath);
            }

            var newWriter = new StreamWriter(stream, Utf8NoBom) { AutoFlush = false };

            this.CurrentFilePath = filePath;
            this.bytesWrittenToCurrentFile = stream.Length;
            this.writer = newWriter;
            this.failureReported = false;
            this.nextOpenAttemptUtc = DateTime.MinValue;

            if (this.maxRetainedFiles > 0)
            {
                RecordCreatedFile(this.directoryKey, Path.GetFileName(filePath));
                this.retainedFiles.Add(filePath);
                this.PruneOldFiles();
            }
            else
            {
                // A file written under unlimited retention is never counted or pruned, even
                // if its name was registered by an earlier bounded sink whose file of that
                // name has since been removed, letting the series reuse the name.
                ForgetCreatedFile(this.directoryKey, Path.GetFileName(filePath));
            }

            this.WriteFilePrologue();

            if (this.announceLogFile && !this.startupMessageEmitted)
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

                ForgetCreatedFile(this.directoryKey, Path.GetFileName(oldest));
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
