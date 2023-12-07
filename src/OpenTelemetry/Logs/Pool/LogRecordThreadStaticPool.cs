// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
            Storage = null;
            return logRecord;
        }

        return new();
    }

    public void Return(LogRecord logRecord)
    {
        if (Storage == null)
        {
            LogRecordPoolHelper.Clear(logRecord);
            Storage = logRecord;
        }
    }
}