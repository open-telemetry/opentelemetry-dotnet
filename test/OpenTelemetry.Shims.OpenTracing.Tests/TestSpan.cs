// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTracing;
using OpenTracing.Tag;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

internal sealed class TestSpan : ISpan
{
    public ISpanContext Context => throw new NotImplementedException();

    public void Finish()
    {
        throw new NotImplementedException();
    }

    public void Finish(DateTimeOffset finishTimestamp)
    {
        throw new NotImplementedException();
    }

    public string GetBaggageItem(string key)
    {
        throw new NotImplementedException();
    }

    public ISpan Log(IEnumerable<KeyValuePair<string, object>> fields)
    {
        throw new NotImplementedException();
    }

    public ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
    {
        throw new NotImplementedException();
    }

    public ISpan Log(string @event)
    {
        throw new NotImplementedException();
    }

    public ISpan Log(DateTimeOffset timestamp, string @event)
    {
        throw new NotImplementedException();
    }

    public ISpan SetBaggageItem(string key, string value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetOperationName(string operationName)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(string key, string value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(string key, bool value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(string key, int value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(string key, double value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(BooleanTag tag, bool value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(IntOrStringTag tag, string value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(IntTag tag, int value)
    {
        throw new NotImplementedException();
    }

    public ISpan SetTag(StringTag tag, string value)
    {
        throw new NotImplementedException();
    }
}
