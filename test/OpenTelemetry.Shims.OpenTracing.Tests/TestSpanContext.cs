// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTracing;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

internal sealed class TestSpanContext : ISpanContext
{
    public string TraceId => throw new NotImplementedException();

    public string SpanId => throw new NotImplementedException();

    public IEnumerable<KeyValuePair<string, string>> GetBaggageItems()
    {
        throw new NotImplementedException();
    }
}
