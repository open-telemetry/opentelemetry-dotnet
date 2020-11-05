// <copyright file="SelfDiagnosticsConfigRefresher.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// SelfDiagnosticsConfigRefresher class checks a location for a configuration file
    /// and open a MemoryMappedFile of a configured size at the configured file path.
    /// The class provides a stream object with proper write position if the configuration
    /// file is present and valid. Otherwise, the stream object would be unavailable,
    /// nothing will be logged to any file.
    /// </summary>
    internal class SelfDiagnosticsConfigRefresher : IDisposable
    {
        private const int FileSizeLowerLimit = 1024;  // Lower limit for log file size in KB: 1MB
        private const int FileSizeUpperLimit = 128 * 1024;  // Upper limit for log file size in KB: 128MB
        private const int ConfigUpdatePeriod = 3000;  // in milliseconds

        /// <summary>
        /// ConfigBufferSize is the maximum bytes of config file that will be read.
        /// </summary>
        private const int ConfigBufferSize = 4 * 1024;
        private const string ConfigFileName = "DiagnosticsConfiguration.json";

        private readonly ThreadLocal<byte[]> configBuffer = new ThreadLocal<byte[]>(() => null);
        private readonly CancellationTokenSource cancellationTokenSource;
        private readonly Task worker;

        /// <summary>
        /// t_memoryMappedFileCache is a handle kept in thread-local storage as a cache to indicate whether the cached
        /// t_viewStream is created from the current m_memoryMappedFile.
        /// </summary>
        private readonly ThreadLocal<MemoryMappedFile> memoryMappedFileCache = new ThreadLocal<MemoryMappedFile>(true);
        private readonly ThreadLocal<MemoryMappedViewStream> viewStream = new ThreadLocal<MemoryMappedViewStream>(true);
        private bool disposedValue;

        // Once the configuration file is valid, an eventListener object will be created.
        // Commented out for now to avoid the "field was never used" compiler error.
        // private SelfDiagnosticsEventListener eventListener;
        private volatile MemoryMappedFile memoryMappedFile;
        private string logDirectory;  // Log directory for log files
        private int logFileSize;  // Log file size in bytes
        private long logFilePosition;  // The logger will write into the byte at this position

        public SelfDiagnosticsConfigRefresher()
        {
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
        public virtual bool TryGetLogStream(int byteCount, out Stream stream, out int availableByteCount)
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
                if (cachedViewStream == null || this.memoryMappedFileCache.Value != this.memoryMappedFile)
                {
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

        internal static bool TryParseLogDirectory(string configJson, out string logDirectory)
        {
            var logDirectoryResult = Regex.Match(configJson, @"""LogDirectory""\s*:\s*""(?<LogDirectory>.*?)""", RegexOptions.IgnoreCase);
            logDirectory = logDirectoryResult.Groups["LogDirectory"].Value;
            return logDirectoryResult.Success && !string.IsNullOrWhiteSpace(logDirectory);
        }

        internal static bool TryParseFileSize(string configJson, out int fileSizeInKB)
        {
            fileSizeInKB = 0;
            var fileSizeResult = Regex.Match(configJson, @"""FileSize""\s*:\s*(?<FileSize>\d+)", RegexOptions.IgnoreCase);
            return fileSizeResult.Success && int.TryParse(fileSizeResult.Groups["FileSize"].Value, out fileSizeInKB);
        }

        private async Task Worker(CancellationToken cancellationToken)
        {
            await Task.Delay(ConfigUpdatePeriod, cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                this.UpdateMemoryMappedFileFromConfiguration();
                await Task.Delay(ConfigUpdatePeriod, cancellationToken).ConfigureAwait(false);
            }
        }

        private void UpdateMemoryMappedFileFromConfiguration()
        {
            if (this.TryGetConfiguration(out string newLogDirectory, out int fileSizeInKB))
            {
                int newFileSize = fileSizeInKB * 1024;
                if (!newLogDirectory.Equals(this.logDirectory) || this.logFileSize != newFileSize)
                {
                    this.CloseLogFile();
                    this.OpenLogFile(newLogDirectory, newFileSize);
                }
            }
            else
            {
                this.CloseLogFile();
            }
        }

        private bool TryGetConfiguration(out string logDirectory, out int fileSizeInKB)
        {
            logDirectory = null;
            fileSizeInKB = 0;
            try
            {
                using FileStream file = File.Open(ConfigFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var buffer = this.configBuffer.Value;
                if (buffer == null)
                {
                    buffer = new byte[ConfigBufferSize]; // TODO: handle OOM
                    this.configBuffer.Value = buffer;
                }

                file.Read(buffer, 0, buffer.Length);
                string configJson = Encoding.UTF8.GetString(buffer);
                if (!TryParseLogDirectory(configJson, out logDirectory))
                {
                    return false;
                }

                if (!TryParseFileSize(configJson, out fileSizeInKB))
                {
                    return false;
                }

                if (fileSizeInKB < FileSizeLowerLimit)
                {
                    fileSizeInKB = FileSizeLowerLimit;
                }

                if (fileSizeInKB > FileSizeUpperLimit)
                {
                    fileSizeInKB = FileSizeUpperLimit;
                }

                return true;
            }
            catch (Exception)
            {
                // do nothing on failure to open/read/parse config file
            }

            return false;
        }

        private void CloseLogFile()
        {
            MemoryMappedFile mmf = Interlocked.CompareExchange(ref this.memoryMappedFile, null, this.memoryMappedFile);
            if (mmf != null)
            {
                foreach (MemoryMappedViewStream stream in this.viewStream.Values)
                {
                    stream.Dispose();
                }

                mmf.Dispose();
            }
        }

        private void OpenLogFile(string newLogDirectory, int newFileSize)
        {
            try
            {
                Directory.CreateDirectory(newLogDirectory);
                var fileName = Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName) + "."
                    + Process.GetCurrentProcess().Id + ".log";
                var filePath = Path.Combine(newLogDirectory, fileName);
                this.memoryMappedFile = MemoryMappedFile.CreateFromFile(filePath, FileMode.Create, null, newFileSize);
                this.logDirectory = newLogDirectory;
                this.logFileSize = newFileSize;
                this.logFilePosition = 0;
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

                    // Ensure worker thread properly finishes.
                    // Or it might have created another MemoryMappedFile in that thread
                    // after the CloseLogFile() below is called.
                    this.CloseLogFile();
                }

                this.disposedValue = true;
            }
        }
    }
}
