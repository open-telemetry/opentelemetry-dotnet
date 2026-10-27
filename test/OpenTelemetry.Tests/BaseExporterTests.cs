// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

public class BaseExporterTests
{
    [Fact]
    public void Verify_ForceFlush_HandlesException()
    {
        // By default, ForceFlush should return true.
        using var testExporter = new DelegatingExporter<object>();
        Assert.True(testExporter.ForceFlush());

        // BaseExporter should catch any exceptions and return false.
        using var exceptionTestExporter = new DelegatingExporter<object>
        {
            OnForceFlushFunc = _ => throw new InvalidOperationException("test exception"),
        };
        Assert.False(exceptionTestExporter.ForceFlush());
    }

    [Theory]
    [InlineData(1_000, 60_000, 1_000)]
    [InlineData(60_000, 1_000, 1_000)]
    [InlineData(1_000, Timeout.Infinite, 1_000)]
    [InlineData(Timeout.Infinite, 1_000, 1_000)]
    [InlineData(0, Timeout.Infinite, Timeout.Infinite)]
    public void Verify_Export_DeadlineOnlyAppliesToThatExport(int timeoutMilliseconds, int nestedTimeoutMilliseconds, int expectedNestedTimeoutMilliseconds)
    {
        DateTime? deadlineUtc = null;
        DateTime? nestedDeadlineUtc = null;
        DateTime? deadlineAfterNestedUtc = null;

        using var exporter = new DelegatingExporter<object>();
        exporter.OnExportFunc = exported =>
        {
            if (deadlineUtc is null)
            {
                deadlineUtc = exporter.ExportDeadlineUtc;

                exporter.Export(exported, nestedTimeoutMilliseconds);
                deadlineAfterNestedUtc = exporter.ExportDeadlineUtc;

                throw new InvalidOperationException("test exception");
            }

            nestedDeadlineUtc = exporter.ExportDeadlineUtc;
            return ExportResult.Success;
        };

        var batch = new Batch<object>([new object()], 1);

        var before = DateTime.UtcNow;
        Assert.Throws<InvalidOperationException>(() => exporter.Export(batch, timeoutMilliseconds));
        var after = DateTime.UtcNow;

        Assert.NotNull(deadlineUtc);
        Assert.NotNull(nestedDeadlineUtc);

        if (timeoutMilliseconds > 0)
        {
            Assert.InRange(deadlineUtc.Value, before.AddMilliseconds(timeoutMilliseconds), after.AddMilliseconds(timeoutMilliseconds));
        }
        else
        {
            Assert.Equal(DateTime.MaxValue, deadlineUtc);
        }

        // A nested export can make the deadline earlier, but not later
        if (expectedNestedTimeoutMilliseconds == Timeout.Infinite)
        {
            Assert.Equal(DateTime.MaxValue, nestedDeadlineUtc);
        }
        else
        {
            Assert.InRange(nestedDeadlineUtc.Value, before.AddMilliseconds(expectedNestedTimeoutMilliseconds), after.AddMilliseconds(expectedNestedTimeoutMilliseconds));
        }

        // Each deadline is restored once its export has finished, even if it throws
        Assert.Equal(deadlineUtc, deadlineAfterNestedUtc);
        Assert.Equal(DateTime.MaxValue, exporter.ExportDeadlineUtc);
    }

    [Fact]
    public void Verify_Shutdown_HandlesSecond()
    {
        // By default, ForceFlush should return true.
        using var testExporter = new DelegatingExporter<object>();
        Assert.True(testExporter.Shutdown());

        // A second Shutdown should return false.
        Assert.False(testExporter.Shutdown());
    }

    [Fact]
    public void Verify_Shutdown_HandlesException()
    {
        // BaseExporter should catch any exceptions and return false.
        using var exceptionTestExporter = new DelegatingExporter<object>
        {
            OnShutdownFunc = _ => throw new InvalidOperationException("test exception"),
        };
        Assert.False(exceptionTestExporter.Shutdown());
    }
}
