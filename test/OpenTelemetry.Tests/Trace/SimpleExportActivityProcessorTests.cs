// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter;
using Xunit;

namespace OpenTelemetry.Trace.Tests;

public class SimpleExportActivityProcessorTests
{
    [Fact]
    public void CheckNullExporter()
    {
        Assert.Throws<ArgumentNullException>(() => new SimpleActivityExportProcessor(null!));
    }

    [Fact]
    public void CheckExportedOnEnd()
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new SimpleActivityExportProcessor(exporter);

        using var activity1 = new Activity("start1")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        processor.OnEnd(activity1);
        Assert.Single(exportedItems);

        using var activity2 = new Activity("start2")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        processor.OnEnd(activity2);
        Assert.Equal(2, exportedItems.Count);
    }

    [Theory]
    [InlineData(Timeout.Infinite)]
    [InlineData(0)]
    [InlineData(1)]
    public void CheckForceFlushExport(int timeout)
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new SimpleActivityExportProcessor(exporter);

        using var activity1 = new Activity("start1")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        using var activity2 = new Activity("start2")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        processor.OnEnd(activity1);
        processor.OnEnd(activity2);

        // checking before force flush
        Assert.Equal(2, exportedItems.Count);

        // forcing flush
        processor.ForceFlush(timeout);
        Assert.Equal(2, exportedItems.Count);
    }

    [Theory]
    [InlineData(Timeout.Infinite)]
    [InlineData(0)]
    [InlineData(1)]
    public void CheckShutdownExport(int timeout)
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new SimpleActivityExportProcessor(exporter);

        using var activity = new Activity("start")
        {
            ActivityTraceFlags = ActivityTraceFlags.Recorded,
        };

        processor.OnEnd(activity);

        // checking before shutdown
        Assert.Single(exportedItems);

        processor.Shutdown(timeout);
        Assert.Single(exportedItems);
    }

    [Fact]
    public void CheckExportForRecordingButNotSampledActivity()
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);
        using var processor = new SimpleActivityExportProcessor(exporter);

        using var activity = new Activity("start")
        {
            ActivityTraceFlags = ActivityTraceFlags.None,
        };

        processor.OnEnd(activity);
        Assert.Empty(exportedItems);
    }
}
