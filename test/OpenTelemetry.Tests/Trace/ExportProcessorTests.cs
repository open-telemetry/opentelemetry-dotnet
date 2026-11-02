// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Trace.Tests;

public class ExportProcessorTests
{
    [Fact]
    public void ExportProcessorIgnoresActivityWhenDropped()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        var sampler = new AlwaysOffSampler();
        var exportedItems = new List<Activity>();
#pragma warning disable CA2000 // Dispose objects before losing scope
        using var processor = new TestActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));
#pragma warning restore CA2000 // Dispose objects before losing scope
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(sampler)
            .AddProcessor(processor)
            .Build();

        using (var activity = activitySource.StartActivity("Activity"))
        {
            Assert.NotNull(activity);
            Assert.False(activity.IsAllDataRequested);
            Assert.Equal(ActivityTraceFlags.RandomTraceId, activity.ActivityTraceFlags);
        }

        Assert.Empty(processor.ExportedItems);
    }

    [Fact]
    public void ExportProcessorIgnoresActivityMarkedAsRecordOnly()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        var sampler = new RecordOnlySampler();
        var exportedItems = new List<Activity>();
#pragma warning disable CA2000 // Dispose objects before losing scope
        using var processor = new TestActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));
#pragma warning restore CA2000 // Dispose objects before losing scope
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(sampler)
            .AddProcessor(processor)
            .Build();

        using (var activity = activitySource.StartActivity("Activity"))
        {
            Assert.NotNull(activity);
            Assert.True(activity.IsAllDataRequested);
            Assert.Equal(ActivityTraceFlags.RandomTraceId, activity.ActivityTraceFlags);
        }

        Assert.Empty(processor.ExportedItems);
    }

    [Fact]
    public void ExportProcessorExportsActivityMarkedAsRecordAndSample()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        var sampler = new AlwaysOnSampler();
        var exportedItems = new List<Activity>();
#pragma warning disable CA2000 // Dispose objects before losing scope
        using var processor = new TestActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems));
#pragma warning restore CA2000 // Dispose objects before losing scope
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(sampler)
            .AddProcessor(processor)
            .Build();

        using (var activity = activitySource.StartActivity("Activity"))
        {
            Assert.NotNull(activity);
            Assert.True(activity.IsAllDataRequested);
            Assert.Equal(ActivityTraceFlags.Recorded | ActivityTraceFlags.RandomTraceId, activity.ActivityTraceFlags);
        }

        Assert.Single(processor.ExportedItems);
    }

    [Fact]
    public void ExportProcessorExportsActivityMarkedAsRecordOnlyWhenExportUnsampledSpansEnabled()
    {
        var activitySourceName = Utils.GetCurrentMethodName();
        var sampler = new RecordOnlySampler();
        var exportedItems = new List<Activity>();
#pragma warning disable CA2000 // Dispose objects before losing scope
        using var processor = new TestActivityExportProcessor(new InMemoryExporter<Activity>(exportedItems))
        {
            ExportUnsampledSpans = true,
        };
#pragma warning restore CA2000 // Dispose objects before losing scope
        using var activitySource = new ActivitySource(activitySourceName);
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(activitySourceName)
            .SetSampler(sampler)
            .AddProcessor(processor)
            .Build();

        using (var activity = activitySource.StartActivity("Activity"))
        {
            Assert.NotNull(activity);
            Assert.True(activity.IsAllDataRequested);
            Assert.Equal(ActivityTraceFlags.RandomTraceId, activity.ActivityTraceFlags);
        }

        Assert.Single(processor.ExportedItems);
    }

    [Fact]
    public void ExportProcessorIgnoresDroppedActivityWhenExportUnsampledSpansEnabled()
    {
        var exportedItems = new List<Activity>();
        using var exporter = new InMemoryExporter<Activity>(exportedItems);

        using var processor = new SimpleActivityExportProcessor(exporter)
        {
            ExportUnsampledSpans = true,
        };

        using var activity = new Activity("Activity")
        {
            IsAllDataRequested = false,
        };

        // Dropped by the sampler: not recording and not sampled.
        // TracerProviderSdk never passes such activities to processors,
        // so OnEnd is called directly.
        processor.OnEnd(activity);

        Assert.Empty(exportedItems);
    }
}
