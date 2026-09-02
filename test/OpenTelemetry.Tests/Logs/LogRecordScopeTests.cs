// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections;
using OpenTelemetry.Logs;

namespace OpenTelemetry.Tests.Logs;

public static class LogRecordScopeTests
{
    [Fact]
    public static void EnumeratorUsesDelegatedEnumeratorForNonReadOnlyListEnumerableScopeTest()
    {
        var innerScope = new Dictionary<string, object?>
        {
            ["item1"] = "value1",
            ["item2"] = "value2",
        };

        var trackingScope = new DisposeTrackingEnumerable(innerScope);

        var items = new List<KeyValuePair<string, object?>>();

        using (var enumerator = new LogRecordScope.Enumerator(trackingScope))
        {
            while (enumerator.MoveNext())
            {
                items.Add(enumerator.Current);
            }
        }

        Assert.Equal(2, items.Count);
        Assert.Contains(new KeyValuePair<string, object?>("item1", "value1"), items);
        Assert.Contains(new KeyValuePair<string, object?>("item2", "value2"), items);

        Assert.Equal(1, trackingScope.EnumeratorsCreated);
        Assert.True(trackingScope.LastEnumeratorDisposed);
    }

    private sealed class DisposeTrackingEnumerable(IEnumerable<KeyValuePair<string, object?>> inner)
        : IEnumerable<KeyValuePair<string, object?>>
    {
        public int EnumeratorsCreated { get; private set; }

        public bool LastEnumeratorDisposed { get; private set; }

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
        {
            this.EnumeratorsCreated++;
            this.LastEnumeratorDisposed = false;
            return new DisposeTrackingEnumerator(inner.GetEnumerator(), () => this.LastEnumeratorDisposed = true);
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();
    }

    private sealed class DisposeTrackingEnumerator(IEnumerator<KeyValuePair<string, object?>> inner, Action onDispose)
        : IEnumerator<KeyValuePair<string, object?>>
    {
        public KeyValuePair<string, object?> Current => inner.Current;

        object IEnumerator.Current => this.Current;

        public bool MoveNext() => inner.MoveNext();

        public void Reset() => inner.Reset();

        public void Dispose()
        {
            onDispose();
            inner.Dispose();
        }
    }
}
