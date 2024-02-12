// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Console.Tests;

public class ConsoleLogRecordExporterTest
{
    [Fact]
    public void VerifyExceptionAttributesAreWritten()
    {
        using var writer = new StringWriter();
        System.Console.SetOut(writer);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("*")
            .ConfigureResource(resource =>
                resource.AddService(
                    serviceName: "Test_VerifyExceptionAttributesAreWritten",
                    serviceVersion: "1.0.0"))
            .AddConsoleExporter()
            .Build();

        using ILoggerFactory factory = LoggerFactory
            .Create(builder => builder.AddOpenTelemetry(n =>
            {
                n.AddConsoleExporter();
            }));

        ILogger logger = factory.CreateLogger("Program");
        try
        {
            int num1 = 5, num2 = 0, division_res = 0;
            division_res = num1 / num2;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "You divided by 0");
        }

        writer.Flush();
        var consoleLog = writer.ToString();

        Assert.Contains("exception.type", consoleLog);
        Assert.Contains("DivideByZeroException", consoleLog);

        Assert.Contains("exception.message", consoleLog);
        Assert.Contains("Attempted to divide by zero.", consoleLog);

        Assert.Contains("exception.stacktrace", consoleLog);
        Assert.Contains("System.DivideByZeroException: Attempted to divide by zero.", consoleLog);
    }
}
