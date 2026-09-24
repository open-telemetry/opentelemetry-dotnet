// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

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
    public void ReferenceCountingDoesNotEndExplicitScope()
    {
        Assert.False(
            Sdk.SuppressInstrumentation,
            "Instrumentation should not be suppressed before entering an explicit scope.");

        using (SuppressInstrumentationScope.Begin())
        {
            Assert.True(
                Sdk.SuppressInstrumentation,
                "The outer scope should suppress instrumentation.");

            using (SuppressInstrumentationScope.Begin(false))
            {
                Assert.False(
                    Sdk.SuppressInstrumentation,
                    "The inner false scope should override outer suppression.");
                Assert.Equal(1, SuppressInstrumentationScope.Enter());
                Assert.True(
                    Sdk.SuppressInstrumentation,
                    "Entering reference-counting mode should suppress instrumentation.");
                Assert.Equal(0, SuppressInstrumentationScope.DecrementIfTriggered());
                Assert.False(
                    Sdk.SuppressInstrumentation,
                    "A zero reference count should retain the inner scope's unsuppressed state.");
            }

            Assert.True(
                Sdk.SuppressInstrumentation,
                "Disposing the inner scope should restore outer suppression.");
        }

        Assert.False(
            Sdk.SuppressInstrumentation,
            "Disposing the outer scope should restore the unsuppressed state.");
    }

    [Fact]
    public void ReferenceCountingSupportsDeepNesting()
    {
        const int ReferenceCount = 32;

        using (SuppressInstrumentationScope.Begin(false))
        {
            Assert.Equal(1, SuppressInstrumentationScope.Enter());

            for (var expectedDepth = 2; expectedDepth <= ReferenceCount; expectedDepth++)
            {
                Assert.Equal(expectedDepth, SuppressInstrumentationScope.IncrementIfTriggered());
            }

            for (var expectedDepth = ReferenceCount - 1; expectedDepth >= 0; expectedDepth--)
            {
                Assert.Equal(expectedDepth, SuppressInstrumentationScope.DecrementIfTriggered());
            }
        }

        Assert.False(
            Sdk.SuppressInstrumentation,
            "Balanced deep reference counts should restore the unsuppressed state.");
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
    public async Task SuppressInstrumentationScopeEnterIsLocalToInheritedAsyncFlow()
        => await VerifyReferenceCountIncrementIsLocalToAsyncFlow(SuppressInstrumentationScope.Enter);

    [Fact]
    public async Task SuppressInstrumentationScopeIncrementIsLocalToInheritedAsyncFlow()
        => await VerifyReferenceCountIncrementIsLocalToAsyncFlow(SuppressInstrumentationScope.IncrementIfTriggered);

    [Fact]
    public async Task SuppressInstrumentationScopeDecrementIsLocalToInheritedAsyncFlow()
    {
        int childDecrementedDepth;
        int childIncrementedDepth;
        int parentDecrementedDepth;
        int parentFinalDepth;
        bool parentIsSuppressed;

        using (SuppressInstrumentationScope.Begin(false))
        {
            Assert.Equal(1, SuppressInstrumentationScope.Enter());
            Assert.Equal(2, SuppressInstrumentationScope.IncrementIfTriggered());

            // Keep the child mutation active until the parent has observed its own depth.
            var childDecremented = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var allowChildToIncrement = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var childTask = Task.Run(async () =>
            {
                childDecremented.SetResult(SuppressInstrumentationScope.DecrementIfTriggered());
                await allowChildToIncrement.Task;
                return SuppressInstrumentationScope.IncrementIfTriggered();
            });

            childDecrementedDepth = await childDecremented.Task;
            parentDecrementedDepth = SuppressInstrumentationScope.DecrementIfTriggered();
            parentIsSuppressed = Sdk.SuppressInstrumentation;
            allowChildToIncrement.SetResult(true);
            childIncrementedDepth = await childTask;
            parentFinalDepth = SuppressInstrumentationScope.DecrementIfTriggered();
        }

        Assert.Equal(1, childDecrementedDepth);
        Assert.Equal(1, parentDecrementedDepth);
        Assert.True(
            parentIsSuppressed,
            "A child decrement should not change the parent's suppression state.");
        Assert.Equal(2, childIncrementedDepth);
        Assert.Equal(0, parentFinalDepth);
        Assert.False(
            Sdk.SuppressInstrumentation,
            "The parent reference count should return to the unsuppressed state.");
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

    private static async Task VerifyReferenceCountIncrementIsLocalToAsyncFlow(Func<int> increment)
    {
        int childIncrementedDepth;
        int childDecrementedDepth;
        int parentDecrementedDepth;
        bool parentIsSuppressed;

        using (SuppressInstrumentationScope.Begin(false))
        {
            Assert.Equal(1, SuppressInstrumentationScope.Enter());

            // Keep the child mutation active until the parent has observed its own depth.
            var childIncremented = new TaskCompletionSource<int>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var allowChildToDecrement = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            var childTask = Task.Run(async () =>
            {
                childIncremented.SetResult(increment());
                await allowChildToDecrement.Task;
                return SuppressInstrumentationScope.DecrementIfTriggered();
            });

            childIncrementedDepth = await childIncremented.Task;
            parentDecrementedDepth = SuppressInstrumentationScope.DecrementIfTriggered();
            allowChildToDecrement.SetResult(true);
            childDecrementedDepth = await childTask;
            parentIsSuppressed = Sdk.SuppressInstrumentation;
        }

        Assert.Equal(2, childIncrementedDepth);
        Assert.Equal(0, parentDecrementedDepth);
        Assert.Equal(1, childDecrementedDepth);
        Assert.False(
            parentIsSuppressed,
            "A child increment should not change the parent's suppression state.");
        Assert.False(
            Sdk.SuppressInstrumentation,
            "The parent reference count should remain in the unsuppressed state.");
    }
}
