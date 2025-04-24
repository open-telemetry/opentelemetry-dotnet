// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Tracing;
using System.IO.MemoryMappedFiles;

namespace OpenTelemetry.Internal;

/// <summary>
/// SelfDiagnosticsConfigRefresher class checks a location for a configuration file
/// and open a MemoryMappedFile of a configured size at the configured file path.
/// The class provides a stream object with proper write position if the configuration
/// file is present and valid. Otherwise, the stream object would be unavailable,
/// nothing will be logged to any file.
/// </summary>
internal class SelfDiagnosticsConfigRefresher : IDisposable
{
    public static readonly byte[] MessageOnNewFile = "If you are seeing this message, it means that the OpenTelemetry SDK has successfully created the log file used to write self-diagnostic logs. This file will be appended with logs as they appear. If you do not see any logs following this line, it means no logs of the configured LogLevel is occurring. You may change the LogLevel to show lower log levels, so that logs of lower severities will be shown.\n"u8.ToArray();

    private const int ConfigurationUpdatePeriodMilliSeconds = 10000;

    private readonly CancellationTokenSource cancellationTokenSource;
    private readonly Task worker;
    private readonly SelfDiagnosticsConfigParser configParser;

    /// <summary>
    /// memoryMappedFileCache is a handle kept in thread-local storage as a cache to indicate whether the cached
    /// viewStream is created from the current m_memoryMappedFile.
    /// </summary>
    private readonly ThreadLocal<MemoryMappedFile> memoryMappedFileCache = new(true);
    private readonly ThreadLocal<MemoryMappedViewStream> viewStream = new(true);
    private bool disposedValue;

    // Once the configuration file is valid, an eventListener object will be created.
    private SelfDiagnosticsEventListener? eventListener;
    private volatile FileStream? underlyingFileStreamForMemoryMappedFile;
    private volatile MemoryMappedFile? memoryMappedFile;
    private string? logDirectory;  // Log directory for log files
    private int logFileSize;  // Log file size in bytes
    private long logFilePosition;  // The logger will write into the byte at this position
    private EventLevel logEventLevel = (EventLevel)(-1);

    public SelfDiagnosticsConfigRefresher()
    {
        this.configParser = new SelfDiagnosticsConfigParser();
        this.UpdateMemoryMappedFileFromConfiguration();
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
    /// Try to get the log stream which is seeked to the position where the next line of log should be written.
    /// </summary>
    /// <param name="byteCount">The number of bytes that need to be written.</param>
    /// <param name="stream">When this method returns, contains the Stream object where `byteCount` of bytes can be written.</param>
    /// <param name="availableByteCount">The number of bytes that is remaining until the end of the stream.</param>
    /// <returns>Whether the logger should log in the stream.</returns>
    public virtual bool TryGetLogStream(
        int byteCount,
        [NotNullWhen(true)]
        out Stream? stream,
        out int availableByteCount)
    {
        if (this.memoryMappedFile == null)
        {
            stream = null;
            availableByteCount = 0;
            return false;
        }

        try
        {
            var cachedViewStream = this.viewStream.Value;

            // Each thread has its own MemoryMappedViewStream created from the only one MemoryMappedFile.
            // Once worker thread updates the MemoryMappedFile, all the cached ViewStream objects become
            // obsolete.
            // Each thread creates a new MemoryMappedViewStream the next time it tries to retrieve it.
            // Whether the MemoryMappedViewStream is obsolete is determined by comparing the current
            // MemoryMappedFile object with the MemoryMappedFile object cached at the creation time of the
            // MemoryMappedViewStream.
            if (cachedViewStream == null || this.memoryMappedFileCache.Value != this.memoryMappedFile)
            {
                // Race condition: The code might reach here right after the worker thread sets memoryMappedFile
                // to null in CloseLogFile().
                // In this case, let the NullReferenceException be caught and fail silently.
                // By design, all events captured will be dropped during a configuration file refresh if
                // the file changed, regardless whether the file is deleted or updated.
                cachedViewStream = this.memoryMappedFile.CreateViewStream();
                this.viewStream.Value = cachedViewStream;
                this.memoryMappedFileCache.Value = this.memoryMappedFile;
            }

            long beginPosition, endPosition;
            do
            {
                beginPosition = this.logFilePosition;
                endPosition = beginPosition + byteCount;
                if (endPosition >= this.logFileSize)
                {
                    endPosition %= this.logFileSize;
                }
            }
            while (beginPosition != Interlocked.CompareExchange(ref this.logFilePosition, endPosition, beginPosition));
            availableByteCount = (int)(this.logFileSize - beginPosition);
            cachedViewStream.Seek(beginPosition, SeekOrigin.Begin);
            stream = cachedViewStream;
            return true;
        }
        catch (Exception)
        {
            stream = null;
            availableByteCount = 0;
            return false;
        }
    }

    private async Task Worker(CancellationToken cancellationToken)
    {
        await Task.Delay(ConfigurationUpdatePeriodMilliSeconds, cancellationToken).ConfigureAwait(false);
        while (!cancellationToken.IsCancellationRequested)
        {
            this.UpdateMemoryMappedFileFromConfiguration();
            await Task.Delay(ConfigurationUpdatePeriodMilliSeconds, cancellationToken).ConfigureAwait(false);
        }
    }

    private void UpdateMemoryMappedFileFromConfiguration()
    {
        if (this.configParser.TryGetConfiguration(out string? newLogDirectory, out int fileSizeInKB, out EventLevel newEventLevel))
        {
            int newFileSize = fileSizeInKB * 1024;
            if (!newLogDirectory.Equals(this.logDirectory, StringComparison.Ordinal) || this.logFileSize != newFileSize)
            {
                this.CloseLogFile();
                this.OpenLogFile(newLogDirectory, newFileSize);
            }

            if (!newEventLevel.Equals(this.logEventLevel))
            {
                if (this.eventListener != null)
                {
                    this.eventListener.Dispose();
                }

                this.eventListener = new SelfDiagnosticsEventListener(newEventLevel, this);
                this.logEventLevel = newEventLevel;
            }
        }
        else
        {
            this.CloseLogFile();
        }
    }

    private void CloseLogFile()
    {
        MemoryMappedFile? mmf = Interlocked.CompareExchange(ref this.memoryMappedFile, null, this.memoryMappedFile);
        if (mmf != null)
        {
            // Each thread has its own MemoryMappedViewStream created from the only one MemoryMappedFile.
            // Once worker thread closes the MemoryMappedFile, all the ViewStream objects should be disposed
            // properly.
            foreach (MemoryMappedViewStream stream in this.viewStream.Values)
            {
                if (stream != null)
                {
                    stream.Dispose();
                }
            }

            mmf.Dispose();
        }

        FileStream? fs = Interlocked.CompareExchange(
            ref this.underlyingFileStreamForMemoryMappedFile,
            null,
            this.underlyingFileStreamForMemoryMappedFile);
        fs?.Dispose();
    }

    private void OpenLogFile(string newLogDirectory, int newFileSize)
    {
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

            // Because the API [MemoryMappedFile.CreateFromFile][1](the string version) behaves differently on
            // .NET Framework and .NET Core, here I am using the [FileStream version][2] of it.
            // Taking the last four parameter values from [.NET Framework]
            // (https://referencesource.microsoft.com/#system.core/System/IO/MemoryMappedFiles/MemoryMappedFile.cs,148)
            // and [.NET Core]
            // (https://github.com/dotnet/runtime/blob/master/src/libraries/System.IO.MemoryMappedFiles/src/System/IO/MemoryMappedFiles/MemoryMappedFile.cs#L152)
            // The parameter for FileAccess is different in type but the same in rules, both are Read and Write.
            // The parameter for FileShare is different in values and in behavior.
            // .NET Framework doesn't allow sharing but .NET Core allows reading by other programs.
            // The last two parameters are the same values for both frameworks.
            // [1]: https://docs.microsoft.com/dotnet/api/system.io.memorymappedfiles.memorymappedfile.createfromfile?view=net-5.0#System_IO_MemoryMappedFiles_MemoryMappedFile_CreateFromFile_System_String_System_IO_FileMode_System_String_System_Int64_
            // [2]: https://docs.microsoft.com/dotnet/api/system.io.memorymappedfiles.memorymappedfile.createfromfile?view=net-5.0#System_IO_MemoryMappedFiles_MemoryMappedFile_CreateFromFile_System_IO_FileStream_System_String_System_Int64_System_IO_MemoryMappedFiles_MemoryMappedFileAccess_System_IO_HandleInheritability_System_Boolean_
            this.underlyingFileStreamForMemoryMappedFile =
                new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read, 0x1000, FileOptions.None);

            // The parameter values for MemoryMappedFileSecurity, HandleInheritability and leaveOpen are the same
            // values for .NET Framework and .NET Core:
            // https://referencesource.microsoft.com/#system.core/System/IO/MemoryMappedFiles/MemoryMappedFile.cs,172
            // https://github.com/dotnet/runtime/blob/master/src/libraries/System.IO.MemoryMappedFiles/src/System/IO/MemoryMappedFiles/MemoryMappedFile.cs#L168-L179
            this.memoryMappedFile = MemoryMappedFile.CreateFromFile(
                this.underlyingFileStreamForMemoryMappedFile,
                null,
                newFileSize,
                MemoryMappedFileAccess.ReadWrite,
                HandleInheritability.None,
                false);
            this.logDirectory = newLogDirectory;
            this.logFileSize = newFileSize;
            this.logFilePosition = MessageOnNewFile.Length;
            using var stream = this.memoryMappedFile.CreateViewStream();
            stream.Write(MessageOnNewFile, 0, MessageOnNewFile.Length);
        }
        catch (Exception ex)
        {
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
                // Or it might have created another MemoryMappedFile in that thread
                // after the CloseLogFile() below is called.
                this.CloseLogFile();

                // Dispose ThreadLocal variables after the file handles are disposed.
                this.viewStream.Dispose();
                this.memoryMappedFileCache.Dispose();
            }

            this.disposedValue = true;
        }
    }
}
