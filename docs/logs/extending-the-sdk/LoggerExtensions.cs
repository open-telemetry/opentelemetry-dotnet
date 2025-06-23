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

#pragma warning disable CA2000 // Dispose objects before losing scope
        return options.AddProcessor(new BatchLogRecordExportProcessor(new MyExporter()));
#pragma warning restore CA2000 // Dispose objects before losing scope
    }
}
