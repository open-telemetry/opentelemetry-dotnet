// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

public class BaseProcessorTests
{
    [Fact]
    public void Verify_ForceFlush_HandlesException()
    {
        // By default, ForceFlush should return true.
        using var testProcessor = new DelegatingProcessor<object>();
        Assert.True(testProcessor.ForceFlush());

        // BaseExporter should catch any exceptions and return false.
        testProcessor.OnForceFlushFunc = _ => throw new InvalidOperationException("test exception");
        Assert.False(testProcessor.ForceFlush());
    }

    [Fact]
    public void Verify_Shutdown_HandlesSecond()
    {
        // By default, Shutdown should return true.
        using var testProcessor = new DelegatingProcessor<object>();
        Assert.True(testProcessor.Shutdown());

        // A second Shutdown should return false.
        Assert.False(testProcessor.Shutdown());
    }

    [Fact]
    public void Verify_Shutdown_HandlesException()
    {
        // BaseExporter should catch any exceptions and return false.
        using var exceptionTestProcessor = new DelegatingProcessor<object>
        {
            OnShutdownFunc = _ => throw new InvalidOperationException("test exception"),
        };
        Assert.False(exceptionTestProcessor.Shutdown());
    }

    [Fact]
    public void NoOp()
    {
        using var testProcessor = new DelegatingProcessor<object>();

        // These two methods are no-op, but account for 7% of the test coverage.
        testProcessor.OnStart(new object());
        testProcessor.OnEnd(new object());
    }
}
