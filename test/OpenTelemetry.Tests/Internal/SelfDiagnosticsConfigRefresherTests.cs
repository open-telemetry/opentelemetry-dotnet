// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Internal.Tests;

public class SelfDiagnosticsConfigRefresherTests
{
    private const string ConfigFilePath = SelfDiagnosticsConfigParser.ConfigFileName;
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

        this.output.WriteLine(logText);  // for debugging in case the test fails
        Assert.StartsWith(MessageOnNewFileString, logText!, StringComparison.Ordinal);

        // The event was captured
        Assert.NotNull(logLine);
        var logMessage = ParseLogMessage(logLine);
        Assert.StartsWith(expectedMessage, logMessage, StringComparison.Ordinal);
    }

    /// <summary>
    /// Creates a refresher configured to write to <paramref name="logDirectory"/> and removes
    /// the configuration file again as soon as it has been read.
    /// </summary>
    private static SelfDiagnosticsConfigRefresher CreateRefresher(string logDirectory)
    {
        CreateConfigFile(logDirectory);

        try
        {
            return new SelfDiagnosticsConfigRefresher();
        }
        finally
        {
            CleanupConfigFile();
        }
    }

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

    private static string ReadLogFile(string logDirectory)
    {
        var outputFileName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName) + "."
#if NET
                + Environment.ProcessId + ".log";
#else
                + Process.GetCurrentProcess().Id + ".log";
#endif
        var outputFilePath = Path.Combine(logDirectory, outputFileName);
        using var file = File.Open(outputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

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

    private static void CreateConfigFile(string logDirectory)
    {
        var configJson = $@"{{
                    ""LogDirectory"": ""{logDirectory}"",
                    ""FileSize"": 1024,
                    ""LogLevel"": ""Error""
                    }}";
        using var file = File.Open(ConfigFilePath, FileMode.Create, FileAccess.Write);
        var configBytes = Encoding.UTF8.GetBytes(configJson);
        file.Write(configBytes, 0, configBytes.Length);
    }

    private static void CleanupConfigFile()
    {
        try
        {
            File.Delete(ConfigFilePath);
        }
        catch
        {
            // ignore any exceptions while removing files
        }
    }
}
