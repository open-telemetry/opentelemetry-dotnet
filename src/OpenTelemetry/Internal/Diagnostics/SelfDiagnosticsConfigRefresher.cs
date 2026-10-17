// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace OpenTelemetry.Internal;

/// <summary>
/// SelfDiagnosticsConfigRefresher class checks a location for a configuration file
/// and opens a log file of a configured size at the configured file path.
/// Events are written in a circular way if the configuration file is present and valid.
/// Otherwise, nothing will be logged to any file.
/// </summary>
#if NET
[System.Runtime.Versioning.UnsupportedOSPlatform("browser")]
#endif
internal class SelfDiagnosticsConfigRefresher : IDisposable
{
    public static readonly byte[] MessageOnNewFile = "If you are seeing this message, it means that the OpenTelemetry SDK has successfully created the log file used to write self-diagnostic logs. This file will be appended with logs as they appear. If you do not see any logs following this line, it means no logs of the configured LogLevel is occurring. You may change the LogLevel to show lower log levels, so that logs of lower severities will be shown.\n"u8.ToArray();

    private const int ConfigurationUpdatePeriodMilliSeconds = 10000;

    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly Task worker;
    private readonly SelfDiagnosticsConfigParser configParser;
    private readonly Func<string, FileStream> logFileFactory;

    private readonly Lock logFileLock = new();
    private bool disposedValue;

    // Once the configuration file is valid, an eventListener object will be created.
    private SelfDiagnosticsEventListener? eventListener;
    private FileStream? logFile;
    private string? logDirectory;  // Log directory for log files
    private int logFileSize;  // Log file size in bytes
    private int logFilePosition;  // The logger will write into the byte at this position
    private EventLevel logEventLevel = (EventLevel)(-1);
    private bool formatMessage;

    public SelfDiagnosticsConfigRefresher()
        : this(static path => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 1, FileOptions.None))
    {
    }

    internal SelfDiagnosticsConfigRefresher(Func<string, FileStream> logFileFactory)
    {
        this.logFileFactory = logFileFactory;
        this.configParser = new SelfDiagnosticsConfigParser();
        this.UpdateLogFileFromConfiguration();
        this.cancellationTokenSource = new CancellationTokenSource();
        this.worker = Task.Run(() => this.Worker(this.cancellationTokenSource.Token), this.cancellationTokenSource.Token);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Writes an event to the log file, wrapping at the configured size.
    /// </summary>
    /// <param name="buffer">The buffer containing the event.</param>
    /// <param name="byteCount">The number of bytes to write from the start of the buffer.</param>
    public virtual void WriteLog(byte[] buffer, int byteCount)
    {
        lock (this.logFileLock)
        {
            if (this.logFile == null)
            {
                return;
            }

            var beginPosition = this.logFilePosition;
            this.logFilePosition = (beginPosition + byteCount) % this.logFileSize;
            var firstByteCount = Math.Min(byteCount, this.logFileSize - beginPosition);

            // Serialize the complete event, including a possible wrap, with other
            // writes and file disposal. One handle is shared by all logging threads.
            this.logFile.Seek(beginPosition, SeekOrigin.Begin);
            this.logFile.Write(buffer, 0, firstByteCount);
            if (firstByteCount < byteCount)
            {
                this.logFile.Seek(0, SeekOrigin.Begin);
                this.logFile.Write(buffer, firstByteCount, byteCount - firstByteCount);
            }
        }
    }

    private async Task Worker(CancellationToken cancellationToken)
    {
        await Task.Delay(ConfigurationUpdatePeriodMilliSeconds, cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            this.UpdateLogFileFromConfiguration();
            await Task.Delay(ConfigurationUpdatePeriodMilliSeconds, cancellationToken).ConfigureAwait(false);
        }
    }

    private void UpdateLogFileFromConfiguration()
    {
        if (this.configParser.TryGetConfiguration(out var newLogDirectory, out var fileSizeInKB, out var newEventLevel, out var formatMessage))
        {
            var newFileSize = fileSizeInKB * 1024;
            if (this.logFile == null || !string.Equals(newLogDirectory, this.logDirectory, StringComparison.Ordinal) || this.logFileSize != newFileSize)
            {
                this.CloseLogFile();
                this.OpenLogFile(newLogDirectory, newFileSize);
            }

            if (!newEventLevel.Equals(this.logEventLevel) || this.formatMessage != formatMessage)
            {
                this.eventListener?.Dispose();
                this.eventListener = new SelfDiagnosticsEventListener(newEventLevel, this, formatMessage);
                this.logEventLevel = newEventLevel;
            }

            this.formatMessage = formatMessage;
        }
        else
        {
            this.CloseLogFile();
        }
    }

    private void CloseLogFile()
    {
        lock (this.logFileLock)
        {
            this.logFile?.Dispose();
            this.logFile = null;
        }
    }

    private void OpenLogFile(string newLogDirectory, int newFileSize)
    {
        FileStream? newLogFile = null;
        try
        {
            Directory.CreateDirectory(newLogDirectory);
            var fileName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "OpenTelemetrySdk") + "."
#if NET
                + Environment.ProcessId + ".log";
#else
                + Process.GetCurrentProcess().Id + ".log";
#endif
            var filePath = Path.Combine(newLogDirectory, fileName);

            // The file name is predictable (executable name + process id), so an entry that already
            // exists under it may be a symbolic link planted by another user of the log directory and
            // must never be opened for writing. Deleting it removes a stale log of an earlier process
            // with the same id, or the link itself but not its target, and FileMode.CreateNew then
            // fails instead of following a link that was (re)created in between.
            File.Delete(filePath);

            // Ordinary file I/O reports a full disk as an exception. Writing to an
            // unbacked memory-mapped page can instead terminate the process.
            // Disable buffering so write errors are caught when the event is logged.
            newLogFile = this.logFileFactory(filePath);
            newLogFile.SetLength(newFileSize);
            newLogFile.Write(MessageOnNewFile, 0, MessageOnNewFile.Length);

            lock (this.logFileLock)
            {
                this.logDirectory = newLogDirectory;
                this.logFileSize = newFileSize;
                this.logFilePosition = MessageOnNewFile.Length;
                this.logFile = newLogFile;
            }
        }
        catch (Exception ex)
        {
            newLogFile?.Dispose();
            OpenTelemetrySdkEventSource.Log.SelfDiagnosticsFileCreateException(newLogDirectory, ex);
        }
    }

    private void Dispose(bool disposing)
    {
        if (!this.disposedValue)
        {
            if (disposing)
            {
                this.cancellationTokenSource.Cancel(false);
                try
                {
                    this.worker.Wait();
                }
                catch (AggregateException)
                {
                }
                finally
                {
                    this.cancellationTokenSource.Dispose();
                }

                // Dispose EventListener before files, because EventListener writes to files.
                this.eventListener?.Dispose();
                this.eventListener = null;

                // Ensure worker thread properly finishes.
                // Or it might have opened another log file in that thread
                // after the CloseLogFile() below is called.
                this.CloseLogFile();
            }

            this.disposedValue = true;
        }
    }
}
