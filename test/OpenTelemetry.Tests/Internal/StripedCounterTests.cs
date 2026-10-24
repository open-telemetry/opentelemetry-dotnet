// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal.Tests;

public static class StripedCounterTests
{
    [Fact]
    public static void IsZeroWhenNothingHasEntered()
    {
        var counter = new StripedCounter();

        Assert.True(counter.IsZero());
    }

    [Fact]
    public static void IsNotZeroWhileAnOperationIsInFlight()
    {
        var counter = new StripedCounter();

        var slot = counter.Enter();

        Assert.InRange(slot, 0, Math.Min(Environment.ProcessorCount, 64) - 1);
        Assert.False(counter.IsZero());

        counter.Exit(slot);

        Assert.True(counter.IsZero());
    }

    [Fact]
    public static void IsNotZeroUntilEveryOperationHasExited()
    {
        var counter = new StripedCounter();

        var slots = new int[8];

        for (var i = 0; i < slots.Length; i++)
        {
            slots[i] = counter.Enter();
        }

        for (var i = 0; i < slots.Length; i++)
        {
            Assert.False(counter.IsZero());
            counter.Exit(slots[i]);
        }

        Assert.True(counter.IsZero());
    }

    [Fact]
    public static async Task IsNotZeroWhileOperationsAreInFlightOnOtherThreads()
    {
        var counter = new StripedCounter();

        var threads = Math.Max(2, Math.Min(Environment.ProcessorCount, 8));

        using var entered = new CountdownEvent(threads);
        using var release = new ManualResetEventSlim();

        var workers = new Task[threads];

        for (var i = 0; i < threads; i++)
        {
            workers[i] = Task.Run(
                () =>
                {
                    var slot = counter.Enter();
                    entered.Signal();
                    release.Wait(TestContext.Current.CancellationToken);
                    counter.Exit(slot);
                },
                TestContext.Current.CancellationToken);
        }

        entered.Wait(TestContext.Current.CancellationToken);

        Assert.False(counter.IsZero());

        release.Set();

        await Task.WhenAll(workers);

        Assert.True(counter.IsZero());
    }

    [Fact]
    public static void IsZeroAfterConcurrentEnterAndExit()
    {
        var counter = new StripedCounter();

        Parallel.For(0, 100_000, _ =>
        {
            var slot = counter.Enter();
            counter.Exit(slot);
        });

        Assert.True(counter.IsZero());
    }
}
