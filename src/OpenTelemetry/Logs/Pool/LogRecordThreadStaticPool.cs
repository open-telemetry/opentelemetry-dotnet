// <copyright file="LogRecordThreadStaticPool.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
