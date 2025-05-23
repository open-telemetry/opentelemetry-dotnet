// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Exporter;
using OpenTelemetry.Tests;
using Xunit;

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
            Assert.Equal(ActivityTraceFlags.None, activity.ActivityTraceFlags);
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
            Assert.Equal(ActivityTraceFlags.None, activity.ActivityTraceFlags);
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
            Assert.Equal(ActivityTraceFlags.Recorded, activity.ActivityTraceFlags);
        }

        Assert.Single(processor.ExportedItems);
    }
}
