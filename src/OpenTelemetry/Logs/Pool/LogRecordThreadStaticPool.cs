// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Logs;

internal sealed class LogRecordThreadStaticPool : ILogRecordPool
{
    [ThreadStatic]
    public static LogRecord? Storage;

    private LogRecordThreadStaticPool()
    {
    }

    public static LogRecordThreadStaticPool Instance { get; } = new();

    public LogRecord Rent()
    {
        var logRecord = Storage;
        if (logRecord != null)
        {
            Debug.Assert(logRecord.Source == LogRecord.LogRecordSource.FromThreadStaticPool, "logRecord.Source was not FromThreadStaticPool");
            Storage = null;
            return logRecord;
        }

        return new()
        {
            Source = LogRecord.LogRecordSource.FromThreadStaticPool,
        };
    }

    public void Return(LogRecord logRecord)
    {
        Debug.Assert(logRecord.Source == LogRecord.LogRecordSource.FromThreadStaticPool, "logRecord.Source was not FromThreadStaticPool");
        if (Storage == null)
        {
            LogRecordPoolHelper.Clear(logRecord);
            Storage = logRecord;
        }
    }
}
