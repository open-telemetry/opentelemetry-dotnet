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

    [Fact]
    public static void Verify_Equals()
    {
        var scope1 = new LogRecordScope("scope");
        Assert.True(scope1.Equals(scope1));
        Assert.True(scope1.Equals((object)scope1));

        // Scope values are compared using object.Equals.
        var scope2 = new LogRecordScope("scope");
        Assert.True(scope1.Equals(scope2));
        Assert.True(scope1.Equals((object)scope2));

        var differentScope = new LogRecordScope("other");
        Assert.False(scope1.Equals(differentScope));
        Assert.False(scope1.Equals((object)differentScope));

        var nullScope1 = new LogRecordScope(null);
        var nullScope2 = new LogRecordScope(null);
        Assert.True(nullScope1.Equals(nullScope2));
        Assert.False(nullScope1.Equals(scope1));

        Assert.False(scope1.Equals(Guid.Empty));
    }

    [Fact]
    public static void Verify_Equals_BoxedValues()
    {
        // Boxing the same value produces different references but equal values.
        var scope1 = new LogRecordScope(42);
        var scope2 = new LogRecordScope(42);
        Assert.True(scope1.Equals(scope2));
    }

    [Fact]
    public static void VerifyOperator_Equals()
    {
        var scope1 = new LogRecordScope("scope");
        var scope2 = new LogRecordScope("scope");
        var scope3 = new LogRecordScope("other");

        Assert.True(scope1 == scope2);
        Assert.False(scope1 == scope3);
    }

    [Fact]
    public static void VerifyOperator_NotEquals()
    {
        var scope1 = new LogRecordScope("scope");
        var scope2 = new LogRecordScope("scope");
        var scope3 = new LogRecordScope("other");

        Assert.False(scope1 != scope2);
        Assert.True(scope1 != scope3);
    }

    [Fact]
    public static void Verify_GetHashCode()
    {
        var scope1 = new LogRecordScope("scope");
        var scope2 = new LogRecordScope("scope");
        var scope3 = new LogRecordScope("other");
        var nullScope = new LogRecordScope(null);

        Assert.Equal(scope1.GetHashCode(), scope2.GetHashCode());
        Assert.NotEqual(scope1.GetHashCode(), scope3.GetHashCode());
        Assert.Equal(0, nullScope.GetHashCode());
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
