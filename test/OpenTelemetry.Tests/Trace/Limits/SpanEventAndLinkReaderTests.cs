// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using static OpenTelemetry.Trace.Tests.SpanLimitTestHelpers;

namespace OpenTelemetry.Trace.Tests;

public class SpanEventAndLinkReaderTests
{
    [Fact]
    public void TryRead_EventsExceedCountLimit_ReturnsAllowedEventsAndCountsDrops()
    {
        var limiter = CreateLimiter(o => o.EventCountLimit = 2);
        using var activity = CreateActivity();
        activity.AddEvent(new ActivityEvent("e1"));
        activity.AddEvent(new ActivityEvent("e2"));
        activity.AddEvent(new ActivityEvent("e3"));

        using var limits = limiter.BeginBatch();
        var events = limits.CreateEventReader(activity);

        var names = new List<string>();
        while (events.TryRead(out var activityEvent))
        {
            names.Add(activityEvent.Name);
        }

        Assert.Equal(["e1", "e2"], names);
        Assert.Equal(1, events.DroppedCount);
    }

    [Fact]
    public void CreateAttributeReader_Event_UsesPerEventLimitAndPreservesStatusKeys()
    {
        var limiter = CreateLimiter(o =>
        {
            o.AttributeCountLimit = 1;
            o.AttributePerEventCountLimit = 2;
            o.AttributeValueLengthLimit = 5;
        });
        using var activity = CreateActivity();
        activity.AddEvent(new ActivityEvent("e1", tags: new ActivityTagsCollection
        {
            ["otel.status_code"] = "ERROR",
            ["b"] = 2,
            ["c"] = 3,
        }));

        using var limits = limiter.BeginBatch();
        var events = limits.CreateEventReader(activity);
        Assert.True(events.TryRead(out _), "Expected to read the first event successfully.");
        var eventAttributes = events.CreateAttributeReader();

        // Status keys are ordinary attributes on events, and the span limit of 1 does not apply.
        Assert.Equal(["otel.status_code", "b"], ReadKeys(ref eventAttributes));
        Assert.Equal(1, eventAttributes.DroppedCount);
        Assert.Equal(5, eventAttributes.ValueLengthLimit);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void CreateAttributeReader_WithoutCurrentItem_ThrowsInvalidOperationException(bool isLink, bool readToEnd)
    {
        var limiter = CreateLimiter();
        using var activity = CreateActivity();
        activity.AddEvent(new ActivityEvent("e1"));
        activity.AddLink(new ActivityLink(default));

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var limits = limiter.BeginBatch();
            if (isLink)
            {
                var links = limits.CreateLinkReader(activity);
                if (readToEnd)
                {
                    Assert.True(links.TryRead(out _), "The single link should be available before advancing the reader to completion.");
                    Assert.False(links.TryRead(out _), "The link reader should complete after its only link has been read.");
                }

                links.CreateAttributeReader();
            }
            else
            {
                var events = limits.CreateEventReader(activity);
                if (readToEnd)
                {
                    Assert.True(events.TryRead(out _), "The single event should be available before advancing the reader to completion.");
                    Assert.False(events.TryRead(out _), "The event reader should complete after its only event has been read.");
                }

                events.CreateAttributeReader();
            }
        });
    }

    [Fact]
    public void DroppedCount_PartialEventRead_CountsRemainingEvents()
    {
        var limiter = CreateLimiter();
        using var activity = CreateActivity();
        activity.AddEvent(new ActivityEvent("e1"));
        activity.AddEvent(new ActivityEvent("e2"));
        activity.AddEvent(new ActivityEvent("e3"));

        using var limits = limiter.BeginBatch();
        var events = limits.CreateEventReader(activity);
        Assert.True(events.TryRead(out _), "Expected to read the first event successfully.");

        Assert.Equal(2, events.DroppedCount);
        Assert.False(events.TryRead(out _), "Expected no more events to be read.");
    }

    [Fact]
    public void CreateAttributeReader_Link_UsesIndependentLinkAndAttributeLimits()
    {
        var limiter = CreateLimiter(o =>
        {
            o.AttributeCountLimit = 1;
            o.LinkCountLimit = 1;
            o.AttributePerLinkCountLimit = 2;
        });
        using var activity = CreateActivity();
        activity.AddLink(new ActivityLink(default, new ActivityTagsCollection { ["a"] = 1, ["b"] = 2, ["c"] = 3 }));
        activity.AddLink(new ActivityLink(default));

        using var limits = limiter.BeginBatch();
        var links = limits.CreateLinkReader(activity);

        Assert.True(links.TryRead(out _), "Expected to read the first link successfully.");
        var linkAttributes = links.CreateAttributeReader();
        Assert.Equal(["a", "b"], ReadKeys(ref linkAttributes));
        Assert.Equal(1, linkAttributes.DroppedCount);

        Assert.False(links.TryRead(out _), "Expected no more links to be read.");
        Assert.Equal(1, links.DroppedCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MarkDropped_EventOrLinkAttribute_FreesCapacityAndPreservesStatusKeys(bool isLink)
    {
        var limiter = CreateLimiter(o =>
        {
            o.AttributeCountLimit = 1;
            o.AttributePerEventCountLimit = 2;
            o.AttributePerLinkCountLimit = 2;
            o.AttributeValueLengthLimit = 5;
        });
        using var activity = CreateActivity();
        var tags = new ActivityTagsCollection
        {
            ["rejected"] = "value",
            ["otel.status_code"] = "ERROR",
            ["otel.status_description"] = "failed",
            ["extra"] = "value",
        };

        using var limits = limiter.BeginBatch();
        AttributeReader attributes;
        if (isLink)
        {
            activity.AddLink(new ActivityLink(default, tags));
            var links = limits.CreateLinkReader(activity);
            Assert.True(links.TryRead(out _), "Expected to read the first link successfully.");
            attributes = links.CreateAttributeReader();
        }
        else
        {
            activity.AddEvent(new ActivityEvent("event", tags: tags));
            var events = limits.CreateEventReader(activity);
            Assert.True(events.TryRead(out _), "Expected to read the first event successfully.");
            attributes = events.CreateAttributeReader();
        }

        Assert.True(attributes.TryRead(out var rejected), "Expected to read the first attribute successfully.");
        Assert.Equal("rejected", rejected.Key);
        attributes.MarkDropped();

        Assert.Equal(["otel.status_code", "otel.status_description"], ReadKeys(ref attributes));
        Assert.Equal(2, attributes.DroppedCount);
        Assert.Equal(5, attributes.ValueLengthLimit);
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(false, 0, 2)]
    [InlineData(false, 2, 0)]
    [InlineData(false, 2, 2)]
    [InlineData(true, 0, 0)]
    [InlineData(true, 0, 2)]
    [InlineData(true, 2, 0)]
    [InlineData(true, 2, 2)]
    public void TryRead_EmptyZeroOrExactItemLimit_ReturnsExpectedItemsAndDrops(bool isLink, int limit, int itemCount)
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter(o =>
        {
            o.EventCountLimit = limit;
            o.LinkCountLimit = limit;
        });
        using var activity = CreateActivity();
        var expectedContexts = new List<ActivityContext>();
        for (var i = 0; i < itemCount; i++)
        {
            activity.AddEvent(new ActivityEvent($"event{i}"));
            var context = new ActivityContext(ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(), ActivityTraceFlags.Recorded);
            expectedContexts.Add(context);
            activity.AddLink(new ActivityLink(context));
        }

        using (var limits = limiter.BeginBatch())
        {
            if (isLink)
            {
                var links = limits.CreateLinkReader(activity);
                var contexts = new List<ActivityContext>();
                while (links.TryRead(out var link))
                {
                    contexts.Add(link.Context);
                }

                Assert.Equal(expectedContexts.Take(limit), contexts);
                Assert.Equal(Math.Max(itemCount - limit, 0), links.DroppedCount);
                Assert.Equal(Math.Max(itemCount - limit, 0), links.DroppedCount);
                Assert.False(links.TryRead(out _), $"The link reader should remain exhausted after processing {itemCount} links with a count limit of {limit}.");
            }
            else
            {
                var events = limits.CreateEventReader(activity);
                var names = new List<string>();
                while (events.TryRead(out var activityEvent))
                {
                    names.Add(activityEvent.Name);
                }

                Assert.Equal(Enumerable.Range(0, Math.Min(limit, itemCount)).Select(i => $"event{i}"), names);
                Assert.Equal(Math.Max(itemCount - limit, 0), events.DroppedCount);
                Assert.Equal(Math.Max(itemCount - limit, 0), events.DroppedCount);
                Assert.False(events.TryRead(out _), $"The event reader should remain exhausted after processing {itemCount} events with a count limit of {limit}.");
            }
        }

        if (itemCount > limit)
        {
            Assert.Equal(
                new SpanLimitWarning("test", 1, 0, isLink ? 0 : itemCount, isLink ? itemCount : 0),
                Assert.Single(GetWarnings(listener)));
        }
        else
        {
            Assert.Empty(GetWarnings(listener));
        }
    }

    [Theory]
    [InlineData(false, 0, 0)]
    [InlineData(false, 0, 2)]
    [InlineData(false, 2, 0)]
    [InlineData(false, 2, 2)]
    [InlineData(true, 0, 0)]
    [InlineData(true, 0, 2)]
    [InlineData(true, 2, 0)]
    [InlineData(true, 2, 2)]
    public void CreateAttributeReader_ConsecutiveItems_AppliesAttributeLimitToEachItem(bool isLink, int limit, int attributeCount)
    {
        var limiter = CreateLimiter(o =>
        {
            o.AttributePerEventCountLimit = limit;
            o.AttributePerLinkCountLimit = limit;
        });
        using var activity = CreateActivity();
        var tags = new ActivityTagsCollection();
        for (var i = 0; i < attributeCount; i++)
        {
            tags.Add($"key{i}", i);
        }

        for (var i = 0; i < 2; i++)
        {
            activity.AddEvent(new ActivityEvent($"event{i}", tags: tags));
            activity.AddLink(new ActivityLink(default, tags));
        }

        using var limits = limiter.BeginBatch();
        var events = limits.CreateEventReader(activity);
        var links = limits.CreateLinkReader(activity);
        for (var i = 0; i < 2; i++)
        {
            AttributeReader attributes;
            if (isLink)
            {
                Assert.True(links.TryRead(out _), $"Link {i} should remain available regardless of the previous link's attribute limit.");
                attributes = links.CreateAttributeReader();
            }
            else
            {
                Assert.True(events.TryRead(out _), $"Event {i} should remain available regardless of the previous event's attribute limit.");
                attributes = events.CreateAttributeReader();
            }

            Assert.Equal(
                Enumerable.Range(0, Math.Min(limit, attributeCount)).Select(j => $"key{j}"),
                ReadKeys(ref attributes));
            Assert.Equal(Math.Max(attributeCount - limit, 0), attributes.DroppedCount);
            Assert.False(attributes.TryRead(out _), $"The attribute reader for {(isLink ? "link" : "event")} {i} should remain exhausted after applying the per-item limit of {limit}.");
        }
    }

    [Fact]
    public void DroppedCount_PartialLinkRead_CountsRemainingLinksOnce()
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter();
        using var activity = CreateActivity();
        for (var i = 0; i < 3; i++)
        {
            activity.AddLink(new ActivityLink(default));
        }

        using (var limits = limiter.BeginBatch())
        {
            var links = limits.CreateLinkReader(activity);
            Assert.True(links.TryRead(out _), "The first link should be available before completing the partially read collection.");
            Assert.Equal(2, links.DroppedCount);
            Assert.Equal(2, links.DroppedCount);
            Assert.False(links.TryRead(out _), "Reading DroppedCount should consume the remaining links and leave the reader exhausted.");
        }

        Assert.Equal(new SpanLimitWarning("test", 1, 0, 0, 2), Assert.Single(GetWarnings(listener)));
    }
}
