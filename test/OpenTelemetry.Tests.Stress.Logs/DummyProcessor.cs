// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Logs;

namespace OpenTelemetry.Tests.Stress;

internal sealed class DummyProcessor : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord record)
    {
    }
}
