// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

public class SuppressInstrumentationTests
{
    [Fact]
    public static void UsingSuppressInstrumentation()
    {
        Assert.False(Sdk.SuppressInstrumentation);

        using (var scope = SuppressInstrumentationScope.Begin())
        {
            Assert.True(Sdk.SuppressInstrumentation);

            using (var innerScope = SuppressInstrumentationScope.Begin())
            {
                innerScope.Dispose();

                Assert.True(Sdk.SuppressInstrumentation);

                scope.Dispose();
            }

            Assert.False(Sdk.SuppressInstrumentation);
        }

        Assert.False(Sdk.SuppressInstrumentation);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    [InlineData(true)]
    public void SuppressInstrumentationBeginTest(bool? shouldBegin)
    {
        Assert.False(Sdk.SuppressInstrumentation);

        using var scope = shouldBegin.HasValue ? SuppressInstrumentationScope.Begin(shouldBegin.Value) : SuppressInstrumentationScope.Begin();
        if (shouldBegin.HasValue)
        {
            Assert.Equal(shouldBegin.Value, Sdk.SuppressInstrumentation);
        }
        else
        {
            Assert.True(Sdk.SuppressInstrumentation); // Default behavior is to pass true and suppress the instrumentation
        }
    }

    [Fact]
    public async Task SuppressInstrumentationScopeEnterIsLocalToAsyncFlow()
    {
        Assert.False(Sdk.SuppressInstrumentation);

        // SuppressInstrumentationScope.Enter called inside the task is only applicable to this async flow

        await Task.Factory.StartNew(
            () =>
            {
                Assert.False(Sdk.SuppressInstrumentation);
                Assert.Equal(1, SuppressInstrumentationScope.Enter());
                Assert.True(Sdk.SuppressInstrumentation);
            },
            CancellationToken.None,
            TaskCreationOptions.None,
            TaskScheduler.Default);

        Assert.False(Sdk.SuppressInstrumentation); // Changes made by SuppressInstrumentationScope.Enter in the task above are not reflected here as it's not part of the same async flow
    }

    [Fact]
    public void DecrementIfTriggeredOnlyWorksInReferenceCountingMode()
    {
        // Instrumentation is not suppressed, DecrementIfTriggered is a no op
        Assert.False(Sdk.SuppressInstrumentation);
        Assert.Equal(0, SuppressInstrumentationScope.DecrementIfTriggered());
        Assert.False(Sdk.SuppressInstrumentation);

        // Instrumentation is suppressed in reference counting mode, DecrementIfTriggered should work
        Assert.Equal(1, SuppressInstrumentationScope.Enter());
        Assert.True(Sdk.SuppressInstrumentation);
        Assert.Equal(0, SuppressInstrumentationScope.DecrementIfTriggered());
        Assert.False(Sdk.SuppressInstrumentation); // Instrumentation is not suppressed anymore
    }

    [Fact]
    public void IncrementIfTriggeredOnlyWorksInReferenceCountingMode()
    {
        // Instrumentation is not suppressed, IncrementIfTriggered is a no op
        Assert.False(Sdk.SuppressInstrumentation);
        Assert.Equal(0, SuppressInstrumentationScope.IncrementIfTriggered());
        Assert.False(Sdk.SuppressInstrumentation);

        // Instrumentation is suppressed in reference counting mode, IncrementIfTriggered should work
        Assert.Equal(1, SuppressInstrumentationScope.Enter());
        Assert.Equal(2, SuppressInstrumentationScope.IncrementIfTriggered());
        Assert.True(Sdk.SuppressInstrumentation);
        Assert.Equal(1, SuppressInstrumentationScope.DecrementIfTriggered());
        Assert.True(Sdk.SuppressInstrumentation); // Instrumentation is still suppressed as IncrementIfTriggered incremented the slot count after Enter, need to decrement the slot count again to enable instrumentation
        Assert.Equal(0, SuppressInstrumentationScope.DecrementIfTriggered());
        Assert.False(Sdk.SuppressInstrumentation); // Instrumentation is not suppressed anymore
    }

    [Fact]
    public void BeginReturnsNoOpWhenAlreadyAlwaysSuppressed()
    {
        Assert.False(Sdk.SuppressInstrumentation);

        using var outer = SuppressInstrumentationScope.Begin(value: true);
        Assert.True(Sdk.SuppressInstrumentation);

        // Nested Begin(true) while already in always-suppress mode should return a no-op
        // disposable and must not alter the suppression state in any way.
        using var inner = SuppressInstrumentationScope.Begin(value: true);
        Assert.True(Sdk.SuppressInstrumentation);

        inner.Dispose();

        // Suppression should still be active after the no-op inner scope is disposed.
        Assert.True(Sdk.SuppressInstrumentation);

        outer.Dispose();
        Assert.False(Sdk.SuppressInstrumentation);
    }

    [Fact]
    public void BeginDisposeIsIdempotent()
    {
        Assert.False(Sdk.SuppressInstrumentation);

        var first = SuppressInstrumentationScope.Begin();
        Assert.True(Sdk.SuppressInstrumentation);

        first.Dispose();
        Assert.False(Sdk.SuppressInstrumentation);

        // Second Dispose() call must be a safe no-op: it must not touch the slot
        // even if the underlying scope instance has since been re-rented from the pool.
        var second = SuppressInstrumentationScope.Begin();
        Assert.True(Sdk.SuppressInstrumentation);

        // Stale reference — must not affect second
        first.Dispose();
        Assert.True(Sdk.SuppressInstrumentation);

        second.Dispose();
        Assert.False(Sdk.SuppressInstrumentation);
    }

    [Fact]
    public void BeginScopesCanBeNestedAndUnwoundCorrectly()
    {
        Assert.False(Sdk.SuppressInstrumentation);

        using var first = SuppressInstrumentationScope.Begin();
        Assert.True(Sdk.SuppressInstrumentation);

        using var second = SuppressInstrumentationScope.Begin(value: false);
        Assert.False(Sdk.SuppressInstrumentation); // second opted out

        using var third = SuppressInstrumentationScope.Begin();
        Assert.True(Sdk.SuppressInstrumentation);

        third.Dispose();
        Assert.False(Sdk.SuppressInstrumentation); // Back to second

        second.Dispose();
        Assert.True(Sdk.SuppressInstrumentation); // Back to first

        first.Dispose();
        Assert.False(Sdk.SuppressInstrumentation);
    }
}
