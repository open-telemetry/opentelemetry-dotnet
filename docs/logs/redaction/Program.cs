// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace Redaction;

public class Program
{
    public static void Main()
    {
        using var loggerFactory = LoggerFactory.Create(builder =>
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(new MyRedactionProcessor());
                options.AddConsoleExporter();
            }));

        var logger = loggerFactory.CreateLogger<Program>();

        // message will be redacted by MyRedactionProcessor
        logger.LogInformation("OpenTelemetry {sensitiveString}.", "<secret>");
    }
}
