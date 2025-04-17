// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Benchmarks.Logs;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;

namespace Benchmarks.Helper;

internal static class LogRecordHelper
{
    internal static LogRecord CreateTestLogRecord()
    {
        var items = new List<LogRecord>(1);
        using var factory = LoggerFactory.Create(builder => builder
            .UseOpenTelemetry(logging =>
            {
                logging.AddInMemoryExporter(items);
            }));

        var logger = factory.CreateLogger("TestLogger");
        logger.HelloFrom("artichoke", 3.99);
        return items[0];
    }
}
