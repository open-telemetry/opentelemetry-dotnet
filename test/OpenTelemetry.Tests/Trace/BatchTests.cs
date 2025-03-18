// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class BatchTests
{
    [Fact]
    public void CheckConstructorExceptions()
    {
        Assert.Throws<ArgumentNullException>(() => new Batch<string>((string[]?)null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Batch<string>(Array.Empty<string>(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Batch<string>(Array.Empty<string>(), 1));

        Assert.Throws<ArgumentNullException>(() => new Batch<string>(null!));
    }

    [Fact]
    public void CheckValidConstructors()
    {
        var value = "a";
        var batch = new Batch<string>(value);
        foreach (var item in batch)
        {
            Assert.Equal(value, item);
        }

        var circularBuffer = new CircularBuffer<string>(1);
        circularBuffer.Add(value);
        batch = new Batch<string>(circularBuffer, 1);
        foreach (var item in batch)
        {
            Assert.Equal(value, item);
        }
    }

    [Fact]
    public void CheckDispose()
    {
        var value = "a";
        var batch = new Batch<string>(value);
        batch.Dispose(); // A test to make sure it doesn't bomb on a null CircularBuffer.

        var circularBuffer = new CircularBuffer<string>(10);
        circularBuffer.Add(value);
        circularBuffer.Add(value);
        circularBuffer.Add(value);
        batch = new Batch<string>(circularBuffer, 10); // Max size = 10
        batch.GetEnumerator().MoveNext();
        Assert.Equal(3, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.RemovedCount);
        batch.Dispose(); // Test anything remaining in the batch is drained when disposed.
        Assert.Equal(3, circularBuffer.AddedCount);
        Assert.Equal(3, circularBuffer.RemovedCount);
        batch.Dispose(); // Verify we don't go into an infinite loop or thrown when empty.

        circularBuffer = new CircularBuffer<string>(10);
        circularBuffer.Add(value);
        circularBuffer.Add(value);
        circularBuffer.Add(value);
        batch = new Batch<string>(circularBuffer, 2); // Max size = 2
        Assert.Equal(3, circularBuffer.AddedCount);
        Assert.Equal(0, circularBuffer.RemovedCount);
        batch.Dispose(); // Test the batch is drained up to max size.
        Assert.Equal(3, circularBuffer.AddedCount);
        Assert.Equal(2, circularBuffer.RemovedCount);
    }

    [Fact]
    public void CheckEnumerator()
    {
        var value = "a";
        var batch = new Batch<string>(value);
        var enumerator = batch.GetEnumerator();
        ValidateEnumerator(enumerator, value);

        var circularBuffer = new CircularBuffer<string>(1);
        circularBuffer.Add(value);
        batch = new Batch<string>(circularBuffer, 1);
        enumerator = batch.GetEnumerator();
        ValidateEnumerator(enumerator, value);
    }

    [Fact]
    public void CheckMultipleEnumerator()
    {
        var value = "a";
        var circularBuffer = new CircularBuffer<string>(10);
        circularBuffer.Add(value);
        circularBuffer.Add(value);
        circularBuffer.Add(value);
        var batch = new Batch<string>(circularBuffer, 10);

        int itemsProcessed = 0;
        foreach (var item in batch)
        {
            itemsProcessed++;
        }

        Assert.Equal(3, itemsProcessed);

        itemsProcessed = 0;
        foreach (var item in batch)
        {
            itemsProcessed++;
        }

        Assert.Equal(0, itemsProcessed);
    }

    [Fact]
    public void CheckEnumeratorResetException()
    {
        var value = "a";
        var batch = new Batch<string>(value);
        var enumerator = batch.GetEnumerator();
        Assert.Throws<NotSupportedException>(() => enumerator.Reset());
    }

    [Fact]
    public void DrainIntoNewBatchTest()
    {
        var circularBuffer = new CircularBuffer<string>(100);
        circularBuffer.Add("a");
        circularBuffer.Add("b");

        Batch<string> batch = new Batch<string>(circularBuffer, 10);

        Assert.Equal(2, batch.Count);

        string[] storage = new string[10];
        int selectedItemCount = 0;
        foreach (string item in batch)
        {
            if (item == "b")
            {
                storage[selectedItemCount++] = item;
            }
        }

        batch = new Batch<string>(storage, selectedItemCount);

        Assert.Equal(1, batch.Count);

        ValidateEnumerator(batch.GetEnumerator(), "b");
    }

    private static void ValidateEnumerator(Batch<string>.Enumerator enumerator, string expected)
    {
        if (enumerator.Current != null)
        {
            Assert.Equal(expected, enumerator.Current);
        }

        if (enumerator.MoveNext())
        {
            Assert.Equal(expected, enumerator.Current);
        }
    }
}
