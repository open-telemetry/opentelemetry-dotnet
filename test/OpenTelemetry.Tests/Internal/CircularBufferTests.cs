// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Internal.Tests;

public class CircularBufferTests
{
    [Fact]
    public void CheckInvalidArgument()
        => Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<string>(0));

    [Fact]
    public void CheckCapacity()
    {
        var capacity = 1;
        var circularBuffer = new CircularBuffer<string>(capacity);

        Assert.Equal(capacity, circularBuffer.Capacity);
    }

    [Fact]
    public void CheckValueWhenAdding()
    {
        var capacity = 1;
        var circularBuffer = new CircularBuffer<string>(capacity);
        var result = circularBuffer.Add("a");
        Assert.True(result);
        Assert.Equal(1, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.Count);
    }

    [Fact]
    public void CheckBufferFull()
    {
        var capacity = 1;
        var circularBuffer = new CircularBuffer<string>(capacity);
        var result = circularBuffer.Add("a");
        Assert.True(result);
        Assert.Equal(1, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.Count);

        result = circularBuffer.Add("b");
        Assert.False(result);
        Assert.Equal(1, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.Count);
    }

    [Fact]
    public void CheckRead()
    {
        var value = "a";
        var capacity = 1;
        var circularBuffer = new CircularBuffer<string>(capacity);
        var result = circularBuffer.Add(value);
        Assert.True(result);
        Assert.Equal(1, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.Count);

        var read = circularBuffer.Read();
        Assert.Equal(value, read);
        Assert.Equal(1, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.RemovedCount);
        Assert.Equal(0, circularBuffer.Count);
    }

    [Fact]
    public void CheckAddedCountAndCount()
    {
        var capacity = 2;
        var circularBuffer = new CircularBuffer<string>(capacity);
        var result = circularBuffer.Add("a");
        Assert.True(result);
        Assert.Equal(1, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.Count);

        result = circularBuffer.Add("a");
        Assert.True(result);
        Assert.Equal(2, circularBuffer.AddedCount);
        Assert.Equal(2, circularBuffer.Count);

        _ = circularBuffer.Read();
        Assert.Equal(2, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.RemovedCount);
        Assert.Equal(1, circularBuffer.Count);
    }

    [Fact]
    public void CheckTryAddReportsCountAfterAdd()
    {
        var circularBuffer = new CircularBuffer<string>(capacity: 3);

        Assert.True(circularBuffer.TryAdd("a", maxSpinCount: 0, out var count));
        Assert.Equal(1, count);

        Assert.True(circularBuffer.TryAdd("b", maxSpinCount: 1, out count));
        Assert.Equal(2, count);

        _ = circularBuffer.Read();

        Assert.True(circularBuffer.TryAdd("c", maxSpinCount: 1, out count));
        Assert.Equal(2, count);

        Assert.True(circularBuffer.TryAdd("d", maxSpinCount: 1, out count));
        Assert.Equal(3, count);

        Assert.False(circularBuffer.TryAdd("e", maxSpinCount: 1, out count));
        Assert.Equal(0, count);
        Assert.Equal(3, circularBuffer.Count);
    }

    [Fact]
    public void CheckTryAddWithoutCountOverload()
    {
        var circularBuffer = new CircularBuffer<string>(capacity: 1);

        Assert.True(circularBuffer.TryAdd("a", maxSpinCount: 1));
        Assert.False(circularBuffer.TryAdd("b", maxSpinCount: 1));
    }

    [Fact]
    public async Task CheckTryAddExceedsMaxSpinCount()
    {
        Assert.SkipWhen(Environment.ProcessorCount < 2, "This machine does not have enough processors to run this test.");

        var circularBuffer = new CircularBuffer<string>(1_000_000);

        using var cts = new CancellationTokenSource();

        var writers = new List<Task>();
        for (var i = 0; i < Environment.ProcessorCount; i++)
        {
            writers.Add(Task.Run(
                () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        circularBuffer.Add("item");
                    }
                },
                TestContext.Current.CancellationToken));
        }

        var exceededMaxSpinCount = false;
        var timeout = Stopwatch.StartNew();

        while (!exceededMaxSpinCount && timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (!circularBuffer.TryAdd("item", maxSpinCount: 1, out var count))
            {
                Assert.Equal(0, count);

                // TryAdd() also returns false if the buffer is full. This is the only
                // reader, so Count cannot have decreased since TryAdd() returned: if
                // the buffer is not full now, then it was not full when TryAdd() failed.
                exceededMaxSpinCount = circularBuffer.Count < circularBuffer.Capacity;
            }

            // Drain the buffer so that the writers never fill it up
            if (!exceededMaxSpinCount && circularBuffer.Count >= circularBuffer.Capacity / 2)
            {
                for (var i = circularBuffer.Count; i > 0; i--)
                {
                    circularBuffer.Read();
                }
            }
        }

#if NET
        await cts.CancelAsync();
#else
        cts.Cancel();
#endif
        await Task.WhenAll(writers);

        Assert.True(exceededMaxSpinCount);
    }

    [Fact]
    public async Task CpuPressureTest()
    {
        Assert.SkipWhen(Environment.ProcessorCount < 2, "This machine does not have enough processors to run this test.");

        var circularBuffer = new CircularBuffer<string>(2048);

        List<Task> tasks = [];

        var numberOfItemsPerWorker = 100_000;

        for (var i = 0; i < Environment.ProcessorCount; i++)
        {
            var tid = i;

            tasks.Add(Task.Run(
                async () =>
                {
                    await Task.Delay(2000);

                    if (tid == 0)
                    {
                        for (var i = 0; i < numberOfItemsPerWorker * (Environment.ProcessorCount - 1); i++)
                        {
                            SpinWait wait = default;
                            while (true)
                            {
                                if (circularBuffer.Count > 0)
                                {
                                    circularBuffer.Read();
                                    break;
                                }

                                wait.SpinOnce();
                            }
                        }
                    }
                    else
                    {
                        for (var i = 0; i < numberOfItemsPerWorker; i++)
                        {
                            SpinWait wait = default;
                            while (true)
                            {
                                if (circularBuffer.Add("item"))
                                {
                                    break;
                                }

                                wait.SpinOnce();
                            }
                        }
                    }
                },
                TestContext.Current.CancellationToken));
        }

        await Task.WhenAll(tasks);
    }
}
