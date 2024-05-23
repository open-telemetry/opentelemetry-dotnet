// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Logs;

internal static class LoggerExtensions
{
    public static LoggerProviderBuilder AddMyExporter(this LoggerProviderBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.AddProcessor(new BatchLogRecordExportProcessor(new MyExporter()));
    }
}
