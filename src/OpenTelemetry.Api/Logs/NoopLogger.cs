// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Logs;

internal sealed class NoopLogger : Logger
{
    public NoopLogger()
        : base(name: null)
    {
    }

    public override void EmitLog(
        in LogRecordData data,
        in LogRecordAttributeList attributes)
    {
    }
}
