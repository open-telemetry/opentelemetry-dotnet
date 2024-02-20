// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleLogRecordExporterTest
{
    [Fact]
    public void VerifyExceptionAttributesAreWritten()
    {
        var originalConsoleOut = System.Console.Out;
        using var writer = new StringWriter();
        System.Console.SetOut(writer);

        using var factory = LoggerFactory
            .Create(builder => builder.AddOpenTelemetry(logging =>
            {
                logging.AddConsoleExporter();
            }));

        var logger = factory.CreateLogger("Program");
        logger.LogError(default, new Exception("Exception Message"), null);

        writer.Flush();
        var consoleLog = writer.ToString();
        System.Console.SetOut(originalConsoleOut);

        Assert.Contains("exception.type", consoleLog);
        Assert.Contains("Exception", consoleLog);

        Assert.Contains("exception.message", consoleLog);
        Assert.Contains("Exception Message", consoleLog);

        Assert.Contains("exception.stacktrace", consoleLog);
        Assert.Contains("Exception: Exception Message", consoleLog);
    }
}
