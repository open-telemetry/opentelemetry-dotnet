// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using OpenTracing.Propagation;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

internal sealed class TestTextMap : ITextMap
{
    public bool GetEnumeratorCalled { get; private set; }

    public bool SetCalled { get; private set; }

    public Dictionary<string, string> Items { get; } = [];

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
