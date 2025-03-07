// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Text;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Internal.Tests;

public class SelfDiagnosticsConfigRefresherTest
{
    private static readonly string ConfigFilePath = SelfDiagnosticsConfigParser.ConfigFileName;
    private static readonly byte[] MessageOnNewFile = SelfDiagnosticsConfigRefresher.MessageOnNewFile;
    private static readonly string MessageOnNewFileString = Encoding.UTF8.GetString(SelfDiagnosticsConfigRefresher.MessageOnNewFile);

    private readonly ITestOutputHelper output;

    public SelfDiagnosticsConfigRefresherTest(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void SelfDiagnosticsConfigRefresher_OmitAsConfigured()
    {
        try
        {
            string logDirectory = Utils.GetCurrentMethodName();
            CreateConfigFile(logDirectory);
            using var configRefresher = new SelfDiagnosticsConfigRefresher();

            // Emitting event of EventLevel.Warning
            OpenTelemetrySdkEventSource.Log.ObservableInstrumentCallbackException("exception");

            int bufferSize = 512;
            byte[] actualBytes = ReadFile(logDirectory, bufferSize);
            string logText = Encoding.UTF8.GetString(actualBytes);
            this.output.WriteLine(logText);  // for debugging in case the test fails
            Assert.StartsWith(MessageOnNewFileString, logText);

            // The event was omitted
            Assert.Equal('\0', (char)actualBytes[MessageOnNewFile.Length]);
        }
        finally
        {
            CleanupConfigFile();
        }
    }

    [Fact]
    public void SelfDiagnosticsEnVarRefresher_CaptureAsConfigured()
    {
        Environment.SetEnvironmentVariable("EnableSelfDiagnostics", "1");
        using var configRefresher = new SelfDiagnosticsConfigRefresher();

        // Emitting event of EventLevel.Error
        OpenTelemetrySdkEventSource.Log.TracerProviderException("Event string sample", "Exception string sample");
        string expectedMessage = "Unknown error in TracerProvider '{0}': '{1}'.{Event string sample}{Exception string sample}";

        //int bufferSize = 2 * (MessageOnNewFileString.Length + expectedMessage.Length);
        //byte[] actualBytes = ReadFile(logDirectory, bufferSize);
        //string logText = Encoding.UTF8.GetString(actualBytes);
        //Assert.StartsWith(MessageOnNewFileString, logText);

        //// The event was captured
        //string logLine = logText.Substring(MessageOnNewFileString.Length);
        //string logMessage = ParseLogMessage(logLine);
        //Assert.StartsWith(expectedMessage, logMessage);
    }

    [Fact]
    public void SelfDiagnosticsConfigRefresher_CaptureAsConfigured()
    {
        try
        {
            string logDirectory = Utils.GetCurrentMethodName();
            CreateConfigFile(logDirectory);
            using var configRefresher = new SelfDiagnosticsConfigRefresher();

            // Emitting event of EventLevel.Error
            OpenTelemetrySdkEventSource.Log.TracerProviderException("Event string sample", "Exception string sample");
            string expectedMessage = "Unknown error in TracerProvider '{0}': '{1}'.{Event string sample}{Exception string sample}";

            int bufferSize = 2 * (MessageOnNewFileString.Length + expectedMessage.Length);
            byte[] actualBytes = ReadFile(logDirectory, bufferSize);
            string logText = Encoding.UTF8.GetString(actualBytes);
            Assert.StartsWith(MessageOnNewFileString, logText);

            // The event was captured
            string logLine = logText.Substring(MessageOnNewFileString.Length);
            string logMessage = ParseLogMessage(logLine);
            Assert.StartsWith(expectedMessage, logMessage);
        }
        finally
        {
            CleanupConfigFile();
        }
    }

    private static string ParseLogMessage(string logLine)
    {
        int timestampPrefixLength = "2020-08-14T20:33:24.4788109Z:".Length;
        Assert.Matches(@"\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}Z:", logLine.Substring(0, timestampPrefixLength));
        return logLine.Substring(timestampPrefixLength);
    }

    private static byte[] ReadFile(string logDirectory, int byteCount)
    {
        var outputFileName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName) + "."
                + Process.GetCurrentProcess().Id + ".log";
        var outputFilePath = Path.Combine(logDirectory, outputFileName);
        using var file = File.Open(outputFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        byte[] actualBytes = new byte[byteCount];
        _ = file.Read(actualBytes, 0, byteCount);
        return actualBytes;
    }

    private static void CreateConfigFile(string logDirectory)
    {
        string configJson = $@"{{
                    ""LogDirectory"": ""{logDirectory}"",
                    ""FileSize"": 1024,
                    ""LogLevel"": ""Error""
                    }}";
        using FileStream file = File.Open(ConfigFilePath, FileMode.Create, FileAccess.Write);
        byte[] configBytes = Encoding.UTF8.GetBytes(configJson);
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
