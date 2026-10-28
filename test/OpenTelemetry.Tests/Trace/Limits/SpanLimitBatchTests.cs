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
    public async Task BeginBatch_ConcurrentBatches_KeepSeparateTotals()
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dispose_SharedBatch_ReportsOnceAndDoesNotCorruptOverlappingBatches(bool disposeAliasFirst)
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1, getTimestamp: AdvancingClock());
        using var small = CreateActivity("kept", "dropped");
        using var large = CreateActivity("kept", "dropped1", "dropped2");
        var original = limiter.BeginBatch();
        var alias = original;
        ReadSpan(original, small);

        if (disposeAliasFirst)
        {
            alias.Dispose();
        }
        else
        {
            original.Dispose();
        }

        var first = limiter.BeginBatch();
        if (disposeAliasFirst)
        {
            original.Dispose();
        }
        else
        {
            alias.Dispose();
        }

        var second = limiter.BeginBatch();
        ReadSpan(first, small);
        ReadSpan(second, large);
        first.Dispose();
        second.Dispose();

        Assert.Equal(
            [
                new SpanLimitWarning("test", 1, 1, 0, 0),
                new SpanLimitWarning("test", 1, 1, 0, 0),
                new SpanLimitWarning("test", 1, 2, 0, 0),
            ],
            GetWarnings(listener));
    }

    [Theory]
    [InlineData("Reset")]
    [InlineData("CreateAttributeReader")]
    [InlineData("CreateEventReader")]
    [InlineData("CreateLinkReader")]
    public void BatchOperation_AfterAliasDisposed_ThrowsAndPreservesNextBatchTotals(string operation)
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1, getTimestamp: AdvancingClock());
        using var small = CreateActivity("kept", "dropped");
        using var large = CreateActivity("kept", "dropped1", "dropped2");

        var exception = Assert.Throws<ObjectDisposedException>(() =>
        {
            var original = limiter.BeginBatch();
            var alias = original;
            ReadSpan(original, small);
            alias.Dispose();

            using var next = limiter.BeginBatch();
            ReadSpan(next, large);

            switch (operation)
            {
                case "Reset":
                    original.Reset();
                    break;
                case "CreateAttributeReader":
                    original.CreateAttributeReader(small);
                    break;
                case "CreateEventReader":
                    original.CreateEventReader(small);
                    break;
                case "CreateLinkReader":
                    original.CreateLinkReader(small);
                    break;
            }
        });

        Assert.Equal(typeof(SpanLimitBatch).FullName, exception.ObjectName);
        Assert.Equal(
            [new SpanLimitWarning("test", 1, 1, 0, 0), new SpanLimitWarning("test", 1, 2, 0, 0)],
            GetWarnings(listener));
    }

    [Theory]
    [InlineData("SpanAttributes.TryRead", false)]
    [InlineData("SpanAttributes.TryRead", true)]
    [InlineData("SpanAttributes.DroppedCount", false)]
    [InlineData("SpanAttributes.DroppedCount", true)]
    [InlineData("SpanAttributes.MarkDropped", false)]
    [InlineData("SpanAttributes.MarkDropped", true)]
    [InlineData("SpanAttributes.Status", false)]
    [InlineData("SpanAttributes.Status", true)]
    [InlineData("SpanAttributes.StatusDescription", false)]
    [InlineData("SpanAttributes.StatusDescription", true)]
    [InlineData("Events.TryRead", false)]
    [InlineData("Events.TryRead", true)]
    [InlineData("Events.DroppedCount", false)]
    [InlineData("Events.DroppedCount", true)]
    [InlineData("Events.CreateAttributeReader", false)]
    [InlineData("Links.TryRead", false)]
    [InlineData("Links.TryRead", true)]
    [InlineData("Links.DroppedCount", false)]
    [InlineData("Links.DroppedCount", true)]
    [InlineData("Links.CreateAttributeReader", false)]
    [InlineData("EventAttributes.TryRead", false)]
    [InlineData("EventAttributes.DroppedCount", false)]
    [InlineData("EventAttributes.MarkDropped", false)]
    [InlineData("LinkAttributes.TryRead", false)]
    [InlineData("LinkAttributes.DroppedCount", false)]
    [InlineData("LinkAttributes.MarkDropped", false)]
    public void ReaderOperation_AfterBatchDisposed_ThrowsObjectDisposedException(string operation, bool completeReaders)
    {
        var limiter = CreateLimiter();
        using var activity = CreateActivity("a");
        activity.SetStatus(ActivityStatusCode.Error, "failed");
        var tags = new ActivityTagsCollection { ["a"] = 1 };
        activity.AddEvent(new ActivityEvent("event", tags: tags));
        activity.AddLink(new ActivityLink(default, tags));

        var exception = Assert.Throws<ObjectDisposedException>(() =>
        {
            var limits = limiter.BeginBatch();
            var attributes = limits.CreateAttributeReader(activity);
            Assert.True(attributes.TryRead(out _), "The span attribute should be available.");

            var events = limits.CreateEventReader(activity);
            Assert.True(events.TryRead(out _), "The event should be available.");
            var eventAttributes = events.CreateAttributeReader();
            Assert.True(eventAttributes.TryRead(out _), "The event attribute should be available.");

            var links = limits.CreateLinkReader(activity);
            Assert.True(links.TryRead(out _), "The link should be available.");
            var linkAttributes = links.CreateAttributeReader();
            Assert.True(linkAttributes.TryRead(out _), "The link attribute should be available.");

            if (completeReaders)
            {
                Assert.False(attributes.TryRead(out _), "The span attribute reader should be exhausted.");
                Assert.False(eventAttributes.TryRead(out _), "The event attribute reader should be exhausted.");
                Assert.False(events.TryRead(out _), "The event reader should be exhausted.");
                Assert.False(linkAttributes.TryRead(out _), "The link attribute reader should be exhausted.");
                Assert.False(links.TryRead(out _), "The link reader should be exhausted.");
            }

            limits.Dispose();

            switch (operation)
            {
                case "SpanAttributes.TryRead":
                    attributes.TryRead(out _);
                    break;
                case "SpanAttributes.DroppedCount":
                    _ = attributes.DroppedCount;
                    break;
                case "SpanAttributes.MarkDropped":
                    attributes.MarkDropped();
                    break;
                case "SpanAttributes.Status":
                    _ = attributes.Status;
                    break;
                case "SpanAttributes.StatusDescription":
                    _ = attributes.StatusDescription;
                    break;
                case "Events.TryRead":
                    events.TryRead(out _);
                    break;
                case "Events.DroppedCount":
                    _ = events.DroppedCount;
                    break;
                case "Events.CreateAttributeReader":
                    events.CreateAttributeReader();
                    break;
                case "Links.TryRead":
                    links.TryRead(out _);
                    break;
                case "Links.DroppedCount":
                    _ = links.DroppedCount;
                    break;
                case "Links.CreateAttributeReader":
                    links.CreateAttributeReader();
                    break;
                case "EventAttributes.TryRead":
                    eventAttributes.TryRead(out _);
                    break;
                case "EventAttributes.DroppedCount":
                    _ = eventAttributes.DroppedCount;
                    break;
                case "EventAttributes.MarkDropped":
                    eventAttributes.MarkDropped();
                    break;
                case "LinkAttributes.TryRead":
                    linkAttributes.TryRead(out _);
                    break;
                case "LinkAttributes.DroppedCount":
                    _ = linkAttributes.DroppedCount;
                    break;
                case "LinkAttributes.MarkDropped":
                    linkAttributes.MarkDropped();
                    break;
            }
        });

        Assert.Equal(typeof(SpanLimitBatch).FullName, exception.ObjectName);
    }

    [Fact]
    public void Reset_AfterExplicitDrop_DiscardsPreviousAttemptTotals()
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1);
        using var activity = CreateActivity("a", "b");

        using (var limits = limiter.BeginBatch())
        {
            var attributes = limits.CreateAttributeReader(activity);
            Assert.True(attributes.TryRead(out _), "The first attribute should be available.");
            attributes.MarkDropped();

            limits.Reset();
            ReadSpan(limits, activity);
        }

        Assert.Equal(new SpanLimitWarning("test", 1, 1, 0, 0), Assert.Single(GetWarnings(listener)));
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
