// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Logs;

internal interface ILogRecordPool
{
    LogRecord Rent();

    void Return(LogRecord logRecord);
}
