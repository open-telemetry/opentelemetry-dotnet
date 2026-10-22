// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using static OpenTelemetry.Trace.Tests.SpanLimitTestHelpers;

namespace OpenTelemetry.Trace.Tests;

public class SpanLimitBatchTests
{
    [Fact]
    public void Dispose_MixedDrops_ReportsAllCountersAndCountsEachSpanOnce()
    {
        using var listener = ListenForWarnings();

        var spanLimiter = CreateLimiter(
            o =>
            {
                o.AttributeCountLimit = 1;
                o.EventCountLimit = 1;
                o.LinkCountLimit = 1;
                o.AttributePerEventCountLimit = 1;
                o.AttributePerLinkCountLimit = 0;
            },
            exporterName: "MyExporter");

        using var span1 = CreateActivity("a", "b", "c");
        span1.AddEvent(new ActivityEvent("e1", tags: new ActivityTagsCollection { ["a"] = 1, ["b"] = 2 }));
        span1.AddEvent(new ActivityEvent("e2", tags: new ActivityTagsCollection { ["ignored"] = 1 }));

        using var span2 = CreateActivity("a");
        span2.AddLink(new ActivityLink(default, new ActivityTagsCollection { ["x"] = 1 }));
        span2.AddLink(new ActivityLink(default, new ActivityTagsCollection { ["ignored"] = 1 }));

        using var cleanSpan = CreateActivity("a");

        using (var limits = spanLimiter.BeginBatch())
        {
            ReadSpan(limits, span1);
            ReadSpan(limits, span2);
            ReadSpan(limits, cleanSpan);
        }

        // Attributes on discarded events/links are not counted a second time.
        var warning = Assert.Single(GetWarnings(listener));
        Assert.Equal(new SpanLimitWarning("MyExporter", 2, 4, 1, 1), warning);
    }

    [Fact]
    public void Dispose_NoDrops_DoesNotEmitWarning()
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter();
        using var activity = CreateActivity("a", "b");

        using (var limits = limiter.BeginBatch())
        {
            ReadSpan(limits, activity);
        }

        Assert.Empty(GetWarnings(listener));
    }

    [Fact]
    public void Reset_BeforeRetry_DiscardsPreviousAttemptTotals()
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1);
        using var activity = CreateActivity("a", "b", "c");

        using (var limits = limiter.BeginBatch())
        {
            // The same span is read twice, as when serialization is retried.
            ReadSpan(limits, activity);
            limits.Reset();
            ReadSpan(limits, activity);
        }

        var warning = Assert.Single(GetWarnings(listener));
        Assert.Equal(1, warning.SpanCount);
        Assert.Equal(2, warning.DroppedAttributeCount);
    }

    [Fact]
    public void Dispose_SuppressedWarnings_ReportsAccumulatedTotalsAtNextInterval()
    {
        using var listener = ListenForWarnings();
        var timestamp = 0L;
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1, getTimestamp: () => timestamp);
        using var activity = CreateActivity("a", "b");

        ReadBatch(limiter, activity);
        ReadBatch(limiter, activity);
        ReadBatch(limiter, activity);
        Assert.Single(GetWarnings(listener));

        timestamp = WarningInterval;
        ReadBatch(limiter, activity);

        var warnings = GetWarnings(listener);
        Assert.Equal(2, warnings.Count);
        Assert.Equal(1, warnings[0].DroppedAttributeCount);
        Assert.Equal(3, warnings[1].SpanCount);
        Assert.Equal(3, warnings[1].DroppedAttributeCount);
    }

    [Fact]
    public void Dispose_CleanBatch_DoesNotConsumeWarningInterval()
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1);
        using var clean = CreateActivity("a");
        using var dropping = CreateActivity("a", "b");

        ReadBatch(limiter, clean);
        ReadBatch(limiter, dropping);

        Assert.Single(GetWarnings(listener));
    }

    [Fact]
    public void Dispose_WarningsDisabled_DoesNotContributeToFutureTotals()
    {
        using var listener = ListenForWarnings();
        var isWarningEnabled = false;
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1, isWarningEnabled: () => isWarningEnabled);
        using var activity = CreateActivity("a", "b", "c");

        ReadBatch(limiter, activity);
        Assert.Empty(GetWarnings(listener));

        // The first warning includes only batches read while the warning was enabled.
        isWarningEnabled = true;
        ReadBatch(limiter, activity);

        var warning = Assert.Single(GetWarnings(listener));
        Assert.Equal(1, warning.SpanCount);
        Assert.Equal(2, warning.DroppedAttributeCount);
    }

    [Fact]
    public void BeginBatch_OverlappingBatches_KeepSeparateTotals()
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1, getTimestamp: AdvancingClock());
        using var small = CreateActivity("a", "b");
        using var large = CreateActivity("a", "b", "c", "d");

        var first = limiter.BeginBatch();
        var second = limiter.BeginBatch();
        ReadSpan(first, small);
        ReadSpan(second, large);
        first.Dispose();
        second.Dispose();

        // The next batch starts with empty totals.
        ReadBatch(limiter, small);

        var warnings = GetWarnings(listener);
        Assert.Equal([1L, 3L, 1L], warnings.Select(w => w.DroppedAttributeCount));
    }

    [Fact]
    public void Dispose_DifferentExporters_ReportsExporterNames()
    {
        using var listener = ListenForWarnings();
        using var activity = CreateActivity("a", "b");

        ReadBatch(CreateLimiter(o => o.AttributeCountLimit = 1, exporterName: "First"), activity);
        ReadBatch(CreateLimiter(o => o.AttributeCountLimit = 1, exporterName: "Second"), activity);

        Assert.Equal(["First", "Second"], GetWarnings(listener).Select(w => w.ExporterName));
    }

    [Fact]
    public async Task BeginBatch_ConcurrentBatches_KeepSeparateTotalsAndReuseClearedState()
    {
        const int CallerCount = 8;
        const int BatchesPerCaller = 20;

        using var listener = ListenForWarnings();

        var activityId = EventSource.CurrentThreadActivityId;
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1, getTimestamp: AdvancingClock());

        using var startGate = new Barrier(CallerCount);

        var tasks = Enumerable.Range(1, CallerCount)
            .Select(dropCount => Task.Factory.StartNew(
                () =>
                {
                    EventSource.SetCurrentThreadActivityId(activityId, out var previousActivityId);
                    try
                    {
                        using var activity = CreateActivity(
                            Enumerable.Range(0, dropCount + 1).Select(i => $"key{i}").ToArray());
                        Assert.True(startGate.SignalAndWait(TimeSpan.FromSeconds(10)), "All exporters should be ready.");
                        for (var i = 0; i < BatchesPerCaller; i++)
                        {
                            ReadBatch(limiter, activity);
                        }
                    }
                    finally
                    {
                        EventSource.SetCurrentThreadActivityId(previousActivityId);
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default))
            .ToArray();

        await Task.WhenAll(tasks);

        var warnings = GetWarnings(listener);

        Assert.Equal(CallerCount * BatchesPerCaller, warnings.Count);
        Assert.Equal(
            Enumerable.Range(1, CallerCount).SelectMany(count => Enumerable.Repeat((long)count, BatchesPerCaller)),
            warnings.Select(warning => warning.DroppedAttributeCount).OrderBy(count => count));
        Assert.All(warnings, warning =>
        {
            Assert.Equal(1, warning.SpanCount);
            Assert.Equal(0, warning.DroppedEventCount);
            Assert.Equal(0, warning.DroppedLinkCount);
        });
    }

    [Fact]
    public void Dispose_CalledTwice_ReportsOnceAndDoesNotCorruptNextBatch()
    {
        using var listener = ListenForWarnings();
        var spanLimiter = CreateLimiter(o => o.AttributeCountLimit = 1, getTimestamp: AdvancingClock());
        using var activity = CreateActivity("a", "b");
        var spanLimitBatch = spanLimiter.BeginBatch();

        ReadSpan(spanLimitBatch, activity);
        spanLimitBatch.Dispose();
        spanLimitBatch.Dispose();
        ReadBatch(spanLimiter, activity);

        Assert.Equal(
            [new SpanLimitWarning("test", 1, 1, 0, 0), new SpanLimitWarning("test", 1, 1, 0, 0)],
            GetWarnings(listener));
    }

    [Fact]
    public void Reset_WithoutRetry_DiscardsAllDropsAndPreservesWarningInterval()
    {
        using var listener = ListenForWarnings();
        var spanLimiter = CreateLimiter(o => o.AttributeCountLimit = 0);
        using var discarded = CreateActivity("a", "b");
        using var exported = CreateActivity("a");
        using (var spanLimitBatch = spanLimiter.BeginBatch())
        {
            ReadSpan(spanLimitBatch, discarded);
            spanLimitBatch.Reset();
        }

        Assert.Empty(GetWarnings(listener));
        ReadBatch(spanLimiter, exported);
        Assert.Equal(new SpanLimitWarning("test", 1, 1, 0, 0), Assert.Single(GetWarnings(listener)));
    }

    [Fact]
    public void BeginBatch_OptionsChangedAfterConstruction_UsesOriginalSnapshot()
    {
        var spanOptions = new SpanLimitOptions
        {
            AttributeCountLimit = 1,
            AttributeValueLengthLimit = 2,
            EventCountLimit = 1,
            LinkCountLimit = 1,
            AttributePerEventCountLimit = 1,
            AttributePerLinkCountLimit = 1,
        };

        var attributeOptions = new AttributeLimitOptions { AttributeCountLimit = 3, AttributeValueLengthLimit = 4 };
        var spanLimiter = new SpanLimiter(spanOptions, attributeOptions, "test");

        spanOptions.AttributeCountLimit = 10;
        spanOptions.AttributeValueLengthLimit = 20;
        spanOptions.EventCountLimit = 10;
        spanOptions.LinkCountLimit = 10;
        spanOptions.AttributePerEventCountLimit = 10;
        spanOptions.AttributePerLinkCountLimit = 10;
        attributeOptions.AttributeCountLimit = 30;
        attributeOptions.AttributeValueLengthLimit = 40;

        using var activity = CreateActivity("a", "b");
        var tags = new ActivityTagsCollection { ["a"] = 1, ["b"] = 2 };

        for (var i = 0; i < 2; i++)
        {
            activity.AddEvent(new ActivityEvent($"event{i}", tags: tags));
            activity.AddLink(new ActivityLink(default, tags));
        }

        using var spanLimitBatch = spanLimiter.BeginBatch();
        Assert.Equal(3, spanLimitBatch.ScopeAttributeCountLimit);
        Assert.Equal(4, spanLimitBatch.ScopeAttributeValueLengthLimit);

        var attributes = spanLimitBatch.CreateAttributeReader(activity);
        Assert.Equal(["a"], ReadKeys(ref attributes));
        Assert.Equal(2, attributes.ValueLengthLimit);
        Assert.Equal(1, attributes.DroppedCount);

        var spanEventReader = spanLimitBatch.CreateEventReader(activity);
        Assert.True(spanEventReader.TryRead(out _), "The original event count limit of 1 should allow the first event.");

        var eventAttributes = spanEventReader.CreateAttributeReader();
        Assert.Equal(["a"], ReadKeys(ref eventAttributes));
        Assert.Equal(2, eventAttributes.ValueLengthLimit);
        Assert.Equal(1, eventAttributes.DroppedCount);
        Assert.False(spanEventReader.TryRead(out _), "The original event count limit of 1 should still exclude the second event after the options are changed.");
        Assert.Equal(1, spanEventReader.DroppedCount);

        var spanLinkReader = spanLimitBatch.CreateLinkReader(activity);
        Assert.True(spanLinkReader.TryRead(out _), "The original link count limit of 1 should allow the first link.");

        var attributeReader = spanLinkReader.CreateAttributeReader();
        Assert.Equal(["a"], ReadKeys(ref attributeReader));
        Assert.Equal(2, attributeReader.ValueLengthLimit);
        Assert.Equal(1, attributeReader.DroppedCount);
        Assert.False(spanLinkReader.TryRead(out _), "The original link count limit of 1 should still exclude the second link after the options are changed.");
        Assert.Equal(1, spanLinkReader.DroppedCount);
    }

    private static void ReadBatch(SpanLimiter limiter, Activity activity)
    {
        using var limits = limiter.BeginBatch();
        ReadSpan(limits, activity);
    }

    private static void ReadSpan(SpanLimitBatch limits, Activity activity)
    {
        var attributes = limits.CreateAttributeReader(activity);
        while (attributes.TryRead(out _))
        {
        }

        var events = limits.CreateEventReader(activity);
        while (events.TryRead(out _))
        {
            var eventAttributes = events.CreateAttributeReader();
            while (eventAttributes.TryRead(out _))
            {
            }
        }

        _ = events.DroppedCount;

        var links = limits.CreateLinkReader(activity);
        while (links.TryRead(out _))
        {
            var linkAttributes = links.CreateAttributeReader();
            while (linkAttributes.TryRead(out _))
            {
            }
        }

        _ = links.DroppedCount;
    }
}
