// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using Xunit;

namespace OpenTelemetry.Tests;

public class BatchTest
{
    [Fact]
    public void CheckConstructorExceptions()
    {
        Assert.Throws<ArgumentNullException>(() => new Batch<string>((string[])null!, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Batch<string>(Array.Empty<string>(), -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Batch<string>(Array.Empty<string>(), 1));
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

    [Fact]
    public void TransformBatchUsingCircularBuffer()
    {
        var circularBuffer = new CircularBuffer<string>(100);
        circularBuffer.Add("a");
        circularBuffer.Add("b");

        Batch<string> batch = new Batch<string>(circularBuffer, 10);

        Assert.Equal(2, batch.Count);
        Assert.NotNull(batch.CircularBuffer);
        Assert.Null(batch.Items);
        Assert.False(batch.Rented);

        object? state = null;

        batch.Transform(
            static (string item, ref object? state) =>
            {
                return item == "a";
            },
            ref state);

        Assert.Equal(0, circularBuffer.Count);
        Assert.Equal(1, batch.Count);
        Assert.Null(batch.CircularBuffer);
        Assert.NotNull(batch.Items);
        Assert.True(batch.Rented);

        var previousRentedArray = batch.Items;

        batch.Transform(
            static (string item, ref object? state) =>
            {
                return false;
            },
            ref state);

        Assert.Equal(0, batch.Count);
        Assert.NotNull(batch.Items);
        Assert.True(batch.Rented);

        Assert.NotEqual(previousRentedArray, batch.Items);

        previousRentedArray = batch.Items;

        batch.Dispose();

        Assert.NotEqual(previousRentedArray, batch.Items);
        Assert.False(batch.Rented);
    }

    [Fact]
    public void TransformBatchUsingCircularBufferOfLogRecords()
    {
        var pool = LogRecordSharedPool.Current;

        var circularBuffer = new CircularBuffer<LogRecord>(100);

        var record = pool.Rent();
        record.CategoryName = "Category1";
        circularBuffer.Add(record);

        record = pool.Rent();
        record.CategoryName = "Category2";
        circularBuffer.Add(record);

        record = pool.Rent();
        record.CategoryName = "Category3";
        circularBuffer.Add(record);

        Batch<LogRecord> batch = new Batch<LogRecord>(circularBuffer, 10);

        Assert.Equal(3, batch.Count);
        Assert.NotNull(batch.CircularBuffer);
        Assert.Null(batch.Items);
        Assert.False(batch.Rented);

        object? state = null;

        batch.Transform(
            static (LogRecord item, ref object? state) =>
            {
                return item.CategoryName != "Category3";
            },
            ref state);

        Assert.Equal(0, circularBuffer.Count);
        Assert.Equal(2, batch.Count);
        Assert.Null(batch.CircularBuffer);
        Assert.NotNull(batch.Items);
        Assert.True(batch.Rented);

        Assert.Equal(1, pool.Count);

        var previousRentedArray = batch.Items;

        batch.Transform(
            static (LogRecord item, ref object? state) =>
            {
                return item.CategoryName != "Category2";
            },
            ref state);

        Assert.Equal(1, batch.Count);
        Assert.NotNull(batch.Items);
        Assert.True(batch.Rented);

        Assert.NotEqual(previousRentedArray, batch.Items);

        previousRentedArray = batch.Items;

        var enumerator = batch.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        enumerator.Dispose();

        Assert.Equal(3, pool.Count);

        batch.Dispose();

        Assert.NotEqual(previousRentedArray, batch.Items);
        Assert.False(batch.Rented);
    }

    [Fact]
    public void TransformBatchEmpty()
    {
        var source = Array.Empty<string>();

        Batch<string> batch = new Batch<string>(source, 0);

        Assert.Equal(0, batch.Count);
        Assert.Null(batch.CircularBuffer);
        Assert.NotNull(batch.Items);
        Assert.False(batch.Rented);

        object? state = null;
        bool transformExecuted = false;

        batch.Transform(
            (string item, ref object? state) =>
            {
                transformExecuted = true;
                return true;
            },
            ref state);

        Assert.False(transformExecuted);

        Assert.Equal(0, batch.Count);
        Assert.Null(batch.CircularBuffer);
        Assert.NotNull(batch.Items);
        Assert.False(batch.Rented);

        Assert.Equal(source, batch.Items);
    }

    [Fact]
    public void TransformBatchSingleItem()
    {
        Batch<string> batch = new Batch<string>("Item");

        Assert.Equal(1, batch.Count);
        Assert.Null(batch.CircularBuffer);
        Assert.Null(batch.Items);
        Assert.NotNull(batch.Item);
        Assert.False(batch.Rented);

        object? state = null;

        batch.Transform(
            (string item, ref object? state) =>
            {
                return true;
            },
            ref state);

        Assert.Equal(1, batch.Count);
        Assert.Null(batch.CircularBuffer);
        Assert.Null(batch.Items);
        Assert.NotNull(batch.Item);
        Assert.False(batch.Rented);

        batch.Transform(
            static (string item, ref object? state) =>
            {
                return false;
            },
            ref state);

        Assert.Equal(0, batch.Count);
        Assert.Null(batch.CircularBuffer);
        Assert.NotNull(batch.Items);
        Assert.Null(batch.Item);
        Assert.False(batch.Rented);
    }

    [Fact]
    public void TransformBatchWithExceptionThrownInDelegate()
    {
        Batch<string> batch = new Batch<string>("Item");

        Assert.Equal(1, batch.Count);

        object? state = null;
        bool transformExecuted = false;

        batch.Transform(
            (string item, ref object? state) =>
            {
                transformExecuted = true;
                throw new InvalidOperationException();
            },
            ref state);

        Assert.True(transformExecuted);
        Assert.Equal(1, batch.Count);
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
