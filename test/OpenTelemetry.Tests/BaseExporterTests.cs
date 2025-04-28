// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

public class BaseExporterTests
{
    [Fact]
    public void Verify_ForceFlush_HandlesException()
    {
        // By default, ForceFlush should return true.
        var testExporter = new DelegatingExporter<object>();
        Assert.True(testExporter.ForceFlush());

        // BaseExporter should catch any exceptions and return false.
        var exceptionTestExporter = new DelegatingExporter<object>
        {
            OnForceFlushFunc = _ => throw new InvalidOperationException("test exception"),
        };
        Assert.False(exceptionTestExporter.ForceFlush());
    }

    [Fact]
    public void Verify_Shutdown_HandlesSecond()
    {
        // By default, ForceFlush should return true.
        var testExporter = new DelegatingExporter<object>();
        Assert.True(testExporter.Shutdown());

        // A second Shutdown should return false.
        Assert.False(testExporter.Shutdown());
    }

    [Fact]
    public void Verify_Shutdown_HandlesException()
    {
        // BaseExporter should catch any exceptions and return false.
        var exceptionTestExporter = new DelegatingExporter<object>
        {
            OnShutdownFunc = _ => throw new InvalidOperationException("test exception"),
        };
        Assert.False(exceptionTestExporter.Shutdown());
    }
}
