// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace Benchmarks.Helper;

internal class LogRecordHelper
{
    internal static LogRecord CreateTestLogRecord()
    {
        var items = new List<LogRecord>(1);
        using var factory = LoggerFactory.Create(builder => builder
            .AddOpenTelemetry(loggerOptions =>
            {
                loggerOptions.AddInMemoryExporter(items);
            }));

        var logger = factory.CreateLogger("TestLogger");
        logger.LogInformation("Hello from {Food} {Price}.", "artichoke", 3.99);
        return items[0];
    }
}