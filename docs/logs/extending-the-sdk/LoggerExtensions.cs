// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Logs;

internal static class LoggerExtensions
{
    public static OpenTelemetryLoggerOptions AddMyExporter(this OpenTelemetryLoggerOptions options)
    {
#if NET
        ArgumentNullException.ThrowIfNull(options);
#else
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }
#endif

        return options.AddProcessor(new BatchLogRecordExportProcessor(new MyExporter()));
    }
}
