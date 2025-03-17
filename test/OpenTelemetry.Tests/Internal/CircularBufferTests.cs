// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class CircularBufferTests
{
    [Fact]
    public void CheckInvalidArgument()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new CircularBuffer<string>(0));
    }

    [Fact]
    public void CheckCapacity()
    {
        int capacity = 1;
        var circularBuffer = new CircularBuffer<string>(capacity);

        Assert.Equal(capacity, circularBuffer.Capacity);
    }

    [Fact]
    public void CheckValueWhenAdding()
    {
        int capacity = 1;
        var circularBuffer = new CircularBuffer<string>(capacity);
        var result = circularBuffer.Add("a");
        Assert.True(result);
        Assert.Equal(1, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.Count);
    }

    [Fact]
    public void CheckBufferFull()
    {
        int capacity = 1;
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
        string value = "a";
        int capacity = 1;
        var circularBuffer = new CircularBuffer<string>(capacity);
        var result = circularBuffer.Add(value);
        Assert.True(result);
        Assert.Equal(1, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.Count);

        string read = circularBuffer.Read();
        Assert.Equal(value, read);
        Assert.Equal(1, circularBuffer.AddedCount);
        Assert.Equal(1, circularBuffer.RemovedCount);
        Assert.Equal(0, circularBuffer.Count);
    }

    [Fact]
    public void CheckAddedCountAndCount()
    {
        int capacity = 2;
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
    public async Task CpuPressureTest()
    {
        if (Environment.ProcessorCount < 2)
        {
            return;
        }

        var circularBuffer = new CircularBuffer<string>(2048);

        List<Task> tasks = new();

        int numberOfItemsPerWorker = 100_000;

        for (int i = 0; i < Environment.ProcessorCount; i++)
        {
            int tid = i;

            tasks.Add(Task.Run(async () =>
            {
                await Task.Delay(2000);

                if (tid == 0)
                {
                    for (int i = 0; i < numberOfItemsPerWorker * (Environment.ProcessorCount - 1); i++)
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
                    for (int i = 0; i < numberOfItemsPerWorker; i++)
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
            }));
        }

        await Task.WhenAll(tasks);
    }
}
