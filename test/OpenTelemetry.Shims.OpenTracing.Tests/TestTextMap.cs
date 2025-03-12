// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using OpenTracing.Propagation;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

internal sealed class TestTextMap : ITextMap
{
    public bool GetEnumeratorCalled { get; private set; }

    public bool SetCalled { get; private set; }

#pragma warning disable IDE0028 // Simplify collection initialization
    public Dictionary<string, string> Items { get; } = new();
#pragma warning restore IDE0028 // Simplify collection initialization

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        this.GetEnumeratorCalled = true;
        return this.Items.GetEnumerator();
    }

    public void Set(string key, string value)
    {
        this.SetCalled = true;
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        throw new NotImplementedException();
    }
}
