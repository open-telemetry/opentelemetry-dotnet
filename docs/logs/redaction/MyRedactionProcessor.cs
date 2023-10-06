// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Redaction;

internal class MyRedactionProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord logRecord)
    {
        if (logRecord.Attributes != null)
        {
            logRecord.Attributes = new MyClassWithRedactionEnumerator(logRecord.Attributes);
        }
    }
}
