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

        // The writers stop adding at this fill level, so the buffer can never be full and
        // a failed TryAdd() below can only mean the spin count was exceeded. (TryAdd() also
        // returns false when the buffer is full, and checking Count afterwards to tell the
        // two apart is a race the writers win whenever they outpace the reader.)
        var writerLimit = circularBuffer.Capacity / 2;

        using var cts = new CancellationTokenSource();

        // Dedicated threads rather than thread pool work items: when the pool is saturated
        // by the rest of the suite running in parallel, queued work items can be delayed for
        // so long that the writers never contend with the reader within the time limit.
        var writers = new List<Thread>();
        for (var i = 0; i < Environment.ProcessorCount; i++)
        {
            var writer = new Thread(() =>
            {
                while (!cts.IsCancellationRequested)
                {
                    if (circularBuffer.Count < writerLimit)
                    {
                        circularBuffer.Add("item");
                    }
                    else
                    {
                        Thread.SpinWait(1);
                    }
                }
            })
            {
                IsBackground = true,
            };

            writer.Start();
            writers.Add(writer);
        }

        var exceededMaxSpinCount = false;
        var timeout = Stopwatch.StartNew();

        while (!exceededMaxSpinCount && timeout.Elapsed < TimeSpan.FromSeconds(30))
        {
            if (!circularBuffer.TryAdd("item", maxSpinCount: 1, out var count))
            {
                Assert.Equal(0, count);
                exceededMaxSpinCount = true;
            }

            // Drain the buffer so that the writers keep adding, and keep contending
            if (!exceededMaxSpinCount && circularBuffer.Count >= writerLimit / 2)
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
        foreach (var writer in writers)
        {
            writer.Join();
        }

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
