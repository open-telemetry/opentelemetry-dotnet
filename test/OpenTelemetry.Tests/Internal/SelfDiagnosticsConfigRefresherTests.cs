// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Internal.Tests;

public class SelfDiagnosticsConfigRefresherTests
{
    private const int CaptureAttempts = 5;

    private static readonly string MessageOnNewFileString = Encoding.UTF8.GetString(SelfDiagnosticsConfigRefresher.MessageOnNewFile);

    private readonly ITestOutputHelper output;

    public SelfDiagnosticsConfigRefresherTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void SelfDiagnosticsConfigRefresher_OmitAsConfigured()
    {
        var logDirectory = Utils.GetCurrentMethodName();
        using var configRefresher = CreateRefresher(logDirectory);

        // Emitting event of EventLevel.Warning
        var omittedEvent = "omitted event sample";
        OpenTelemetrySdkEventSource.Log.ObservableInstrumentCallbackException(omittedEvent);

        var logText = ReadLogFile(logDirectory);
        this.output.WriteLine(logText);  // for debugging in case the test fails
        Assert.StartsWith(MessageOnNewFileString, logText, StringComparison.Ordinal);

        // The event was omitted. Error level events emitted elsewhere in the process can end up
        // in this file, so assert on the absence of this event rather than on the file holding
        // nothing but the header.
        Assert.DoesNotContain(omittedEvent, logText, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfDiagnosticsConfigRefresher_CaptureAsConfigured()
    {
        var logDirectory = Utils.GetCurrentMethodName();
        using var configRefresher = CreateRefresher(logDirectory);

        var expectedMessage = "Unknown error in TracerProvider '{0}': '{1}'.{Event string sample}{Exception string sample}";

        string? logText = null;
        string? logLine = null;

        for (var attempt = 0; attempt < CaptureAttempts && logLine == null; attempt++)
        {
            // Emitting event of EventLevel.Error
            OpenTelemetrySdkEventSource.Log.TracerProviderException("Event string sample", "Exception string sample");

            logText = ReadLogFile(logDirectory);
            logLine = FindLogLine(logText, expectedMessage);
        }

        this.output.WriteLine(logText ?? string.Empty);  // for debugging in case the test fails
        Assert.StartsWith(MessageOnNewFileString, logText!, StringComparison.Ordinal);

        // The event was captured
        Assert.NotNull(logLine);
        var logMessage = ParseLogMessage(logLine);
        Assert.StartsWith(expectedMessage, logMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfDiagnosticsConfigRefresher_ReplacesExistingLogFile()
    {
        var logDirectory = Utils.GetCurrentMethodName();
        var logFilePath = GetLogFilePath(logDirectory);

        Directory.CreateDirectory(logDirectory);
        File.WriteAllText(logFilePath, "stale content");

        using var configRefresher = CreateRefresher(logDirectory);

        Assert.StartsWith(MessageOnNewFileString, ReadLogFile(logDirectory), StringComparison.Ordinal);
    }

#if NET
    [Fact]
    public void SelfDiagnosticsConfigRefresher_DoesNotFollowLink()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Creating symbolic links requires elevated privileges on Windows.");

        var logDirectory = Utils.GetCurrentMethodName();
        var logFilePath = GetLogFilePath(logDirectory);
        var linkTarget = Path.GetFullPath(Path.Combine(logDirectory, "target.txt"));

        Directory.CreateDirectory(logDirectory);
        File.Delete(logFilePath);
        File.WriteAllText(linkTarget, "target content");
        File.CreateSymbolicLink(logFilePath, linkTarget);

        using var configRefresher = CreateRefresher(logDirectory);

        // The link was replaced by the log file and its target was left alone.
        Assert.Equal("target content", File.ReadAllText(linkTarget));
        Assert.Null(new FileInfo(logFilePath).LinkTarget);
        Assert.StartsWith(MessageOnNewFileString, ReadLogFile(logDirectory), StringComparison.Ordinal);
    }
#endif

    [Fact]
    public void SelfDiagnosticsConfigRefresher_CaptureAfterLogFileTruncated()
    {
        var logDirectory = Utils.GetCurrentMethodName();
        FileStream? file = null;
        using var configRefresher = CreateRefresher(
            logDirectory,
            logFileFactory: path => file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1, FileOptions.None));
        Assert.NotNull(file);
        using var listener = new SelfDiagnosticsEventListener(EventLevel.Error, configRefresher);
        listener.WriteEvent("Before truncation", null);

        // Use the owning handle because Windows denies opening a second writer.
        // A memory mapping prevents shrinking this file on Windows and makes the
        // next log write fatal on Unix if another process truncates the file.
        file.SetLength(0);

        const string ExpectedMessage = "After truncation";
        listener.WriteEvent(ExpectedMessage, null);

        Assert.Contains(ExpectedMessage + "\n", ReadLogFile(logDirectory), StringComparison.Ordinal);
    }

    [Fact]
    public void SelfDiagnosticsConfigRefresher_DropFailedWriteAndResume()
    {
        var logDirectory = Utils.GetCurrentMethodName();
        FailingFileStream? failingFile = null;
        using var configRefresher = CreateRefresher(logDirectory, EventLevel.Critical, path => failingFile = new FailingFileStream(path));
        Assert.NotNull(failingFile);
        using var listener = new SelfDiagnosticsEventListener(EventLevel.Critical, configRefresher);

        // Inject the IOException reported by ordinary file I/O on a full disk.
        // The listener must drop this event and still accept the next event.
        failingFile.FailWrites = true;
        var initialWriteAttempts = failingFile.WriteAttempts;
        listener.WriteEvent("Dropped event", null);
        Assert.Equal(initialWriteAttempts + 1, failingFile.WriteAttempts);

        failingFile.FailWrites = false;
        listener.WriteEvent("Recovered event", null);
        Assert.Equal(initialWriteAttempts + 2, failingFile.WriteAttempts);

        var logText = ReadLogFile(logDirectory);
        Assert.DoesNotContain("Dropped event", logText, StringComparison.Ordinal);
        Assert.Contains("Recovered event\n", logText, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfDiagnosticsConfigRefresher_WrapAtConfiguredSize()
    {
        var logDirectory = Utils.GetCurrentMethodName();
        using var configRefresher = CreateRefresher(logDirectory, EventLevel.Critical);
        var filePath = GetLogFilePath(logDirectory);
        var fileSize = (int)new FileInfo(filePath).Length;
        var padding = new byte[fileSize - SelfDiagnosticsConfigRefresher.MessageOnNewFile.Length - 2];
        configRefresher.WriteLog(padding, padding.Length);

        var buffer = "abcd"u8.ToArray();
        configRefresher.WriteLog(buffer, buffer.Length);
        configRefresher.WriteLog("z"u8.ToArray(), 1);

        var logText = ReadLogFile(logDirectory);
        Assert.Equal(fileSize, new FileInfo(filePath).Length);
        Assert.StartsWith("cdz", logText, StringComparison.Ordinal);
        Assert.EndsWith("ab", logText, StringComparison.Ordinal);
    }

    [Fact]
    public void SelfDiagnosticsConfigRefresher_ConcurrentWrites()
    {
        var logDirectory = Utils.GetCurrentMethodName();
        using var configRefresher = CreateRefresher(logDirectory, EventLevel.Critical);
        var expected = Enumerable.Range(0, 256)
            .Select(value => value.ToString("D4", CultureInfo.InvariantCulture))
            .ToArray();

        Parallel.ForEach(expected, message =>
        {
            var buffer = Encoding.UTF8.GetBytes(message + "\n");
            configRefresher.WriteLog(buffer, buffer.Length);
        });

        var actual = ReadLogFile(logDirectory).Substring(MessageOnNewFileString.Length)
            .Split(['\n'], StringSplitOptions.RemoveEmptyEntries)
            .OrderBy(message => message, StringComparer.Ordinal);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void SelfDiagnosticsConfigRefresher_DisposeReleasesLogFile()
    {
        var logDirectory = Utils.GetCurrentMethodName();
        using var configRefresher = CreateRefresher(logDirectory, EventLevel.Critical);
        var filePath = GetLogFilePath(logDirectory);

        configRefresher.Dispose();
        configRefresher.WriteLog("After disposal"u8.ToArray(), 14);

        using var exclusiveFile = File.Open(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Equal(1024 * 1024, exclusiveFile.Length);
    }

    [Fact]
    public void SelfDiagnosticsConfigRefresher_PreserveWindowsFileSharing()
    {
        Assert.SkipUnless(RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "This test can only be run on Windows.");

        var logDirectory = Utils.GetCurrentMethodName();
        using var configRefresher = CreateRefresher(logDirectory, EventLevel.Critical);
        var filePath = GetLogFilePath(logDirectory);

        using var reader = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        Assert.Throws<IOException>(() =>
        {
            using var writer = File.Open(filePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
        });
    }

    /// <summary>
    /// Creates a refresher configured to write to <paramref name="logDirectory"/> and removes
    /// the configuration file again as soon as it has been read.
    /// </summary>
    private static SelfDiagnosticsConfigRefresher CreateRefresher(
        string logDirectory,
        EventLevel logLevel = EventLevel.Error,
        Func<string, FileStream>? logFileFactory = null)
    {
        // Each test uses its own configuration file rather than the process-wide
        // OTEL_DIAGNOSTICS.json: the SDK's own SelfDiagnostics singleton polls that
        // file from the working directory too, and if a poll lands while a test's
        // configuration exists it opens the same log file as the test's refresher.
        var configFilePath = GetConfigFilePath(logDirectory);

        CreateConfigFile(configFilePath, logDirectory, logLevel);

        try
        {
            return new SelfDiagnosticsConfigRefresher(
                logFileFactory ?? SelfDiagnosticsConfigRefresher.DefaultLogFileFactory,
                configFilePath);
        }
        finally
        {
            CleanupConfigFile(configFilePath);
        }
    }

    private static string GetConfigFilePath(string logDirectory)
        => logDirectory + "." + SelfDiagnosticsConfigParser.ConfigFileName;

    private static string? FindLogLine(string logText, string expectedMessage)
    {
        if (logText.Length <= MessageOnNewFileString.Length)
        {
            return null;
        }

        var lines = logText.Substring(MessageOnNewFileString.Length).Split('\n');

        foreach (var line in lines)
        {
            if (line.Contains(expectedMessage, StringComparison.Ordinal))
            {
                return line;
            }
        }

        return null;
    }

    private static string ParseLogMessage(string logLine)
    {
        var timestampPrefixLength = "2020-08-14T20:33:24.4788109Z:".Length;
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z:", logLine.Substring(0, timestampPrefixLength));
        return logLine.Substring(timestampPrefixLength);
    }

    private static string GetLogFilePath(string logDirectory)
    {
        var outputFileName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName ?? "OpenTelemetrySdk") + "."
#if NET
                + Environment.ProcessId + ".log";
#else
                + Process.GetCurrentProcess().Id + ".log";
#endif
        return Path.Combine(logDirectory, outputFileName);
    }

    private static string ReadLogFile(string logDirectory)
    {
        using var file = File.Open(GetLogFilePath(logDirectory), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

        var actualBytes = new byte[file.Length];

        var totalBytesRead = 0;
        int bytesRead;

        while (totalBytesRead < actualBytes.Length &&
               (bytesRead = file.Read(actualBytes, totalBytesRead, actualBytes.Length - totalBytesRead)) > 0)
        {
            totalBytesRead += bytesRead;
        }

        // The log file is a fixed size circular buffer, so trim the unwritten remainder.
        return Encoding.UTF8.GetString(actualBytes, 0, totalBytesRead).TrimEnd('\0');
    }

    private static void CreateConfigFile(string configFilePath, string logDirectory, EventLevel logLevel)
    {
        var configJson = $@"{{
                    ""LogDirectory"": ""{logDirectory}"",
                    ""FileSize"": 1024,
                    ""LogLevel"": ""{logLevel}""
                    }}";
        using var file = File.Open(configFilePath, FileMode.Create, FileAccess.Write);
        var configBytes = Encoding.UTF8.GetBytes(configJson);
        file.Write(configBytes, 0, configBytes.Length);
    }

    private static void CleanupConfigFile(string configFilePath)
    {
        try
        {
            File.Delete(configFilePath);
        }
        catch
        {
            // ignore any exceptions while removing files
        }
    }

    private sealed class FailingFileStream(string path)
        : FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read, 1, FileOptions.None)
    {
        public bool FailWrites { get; set; }

        public int WriteAttempts { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.WriteAttempts++;
            if (this.FailWrites)
            {
                throw new IOException("Simulated full disk.");
            }

            base.Write(buffer, offset, count);
        }
    }
}
