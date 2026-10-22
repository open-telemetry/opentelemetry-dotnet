// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using static OpenTelemetry.Trace.Tests.SpanLimitTestHelpers;

namespace OpenTelemetry.Trace.Tests;

public class SpanAttributeReaderTests
{
    [Fact]
    public void TryRead_AttributesExceedCountLimit_ReturnsAllowedAttributesAndCountsDrops()
    {
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 3);
        using var activity = CreateActivity("a", "b", "c", "d", "e");

        using var limits = limiter.BeginBatch();
        var attributes = limits.CreateAttributeReader(activity);

        Assert.Equal(["a", "b", "c"], ReadKeys(ref attributes));
        Assert.Equal(2, attributes.DroppedCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0)]
    [InlineData(7)]
    public void ValueLengthLimit_Configured_ExposesWriterLimit(int? valueLengthLimit)
    {
        var limiter = CreateLimiter(o => o.AttributeValueLengthLimit = valueLengthLimit);
        using var activity = CreateActivity("a");

        using var limits = limiter.BeginBatch();
        var attributes = limits.CreateAttributeReader(activity);

        Assert.Equal(valueLengthLimit, attributes.ValueLengthLimit);
    }

    [Fact]
    public void TryRead_StatusTagsAfterCountLimit_ExcludesTagsAndResolvesStatus()
    {
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1);
        using var activity = CreateActivity("a", "b");
        activity.SetTag("otel.status_code", "ERROR");
        activity.SetTag("otel.status_description", "failed");

        using var limits = limiter.BeginBatch();
        var attributes = limits.CreateAttributeReader(activity);

        Assert.Equal(["a"], ReadKeys(ref attributes));
        Assert.Equal(1, attributes.DroppedCount);
        Assert.Equal(ActivityStatusCode.Error, attributes.Status);
        Assert.Equal("failed", attributes.StatusDescription);
    }

    [Theory]
    [InlineData(ActivityStatusCode.Unset, null, null, null, ActivityStatusCode.Unset, null)]
    [InlineData(ActivityStatusCode.Unset, null, "ERROR", "tag", ActivityStatusCode.Error, "tag")]
    [InlineData(ActivityStatusCode.Unset, null, "error", "tag", ActivityStatusCode.Error, "tag")]
    [InlineData(ActivityStatusCode.Unset, null, "OK", "tag", ActivityStatusCode.Ok, null)]
    [InlineData(ActivityStatusCode.Unset, null, "UNSET", "tag", ActivityStatusCode.Unset, null)]
    [InlineData(ActivityStatusCode.Unset, null, "unknown", "tag", ActivityStatusCode.Unset, null)]
    [InlineData(ActivityStatusCode.Error, "activity", "OK", "tag", ActivityStatusCode.Error, "activity")]
    [InlineData(ActivityStatusCode.Ok, null, "ERROR", "tag", ActivityStatusCode.Ok, null)]
    public void Status_ActivityAndLegacyTags_ResolvesStatusAndDescription(
        ActivityStatusCode activityStatus,
        string? activityDescription,
        string? statusCodeTag,
        string? statusDescriptionTag,
        ActivityStatusCode expectedStatus,
        string? expectedDescription)
    {
        var limiter = CreateLimiter();
        using var activity = CreateActivity("a");
        activity.SetStatus(activityStatus, activityDescription);
        activity.SetTag("otel.status_code", statusCodeTag);
        activity.SetTag("otel.status_description", statusDescriptionTag);

        using var limits = limiter.BeginBatch();
        var attributes = limits.CreateAttributeReader(activity);

        Assert.Equal(["a"], ReadKeys(ref attributes));
        Assert.Equal(expectedStatus, attributes.Status);
        Assert.Equal(expectedDescription, attributes.StatusDescription);
    }

    [Fact]
    public void MarkDropped_AfterSuccessfulRead_FreesCapacityForNextAttribute()
    {
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 2);
        using var activity = CreateActivity("a", "b", "c", "d");

        using var limits = limiter.BeginBatch();
        var attributes = limits.CreateAttributeReader(activity);

        var written = new List<string>();
        while (attributes.TryRead(out var attribute))
        {
            if (attribute.Key == "b")
            {
                attributes.MarkDropped();
                continue;
            }

            written.Add(attribute.Key);
        }

        Assert.Equal(["a", "c"], written);
        Assert.Equal(2, attributes.DroppedCount);
    }

    [Theory]
    [InlineData("BeforeRead")]
    [InlineData("AfterMarkDropped")]
    [InlineData("AfterCompletion")]
    [InlineData("AfterDroppedCount")]
    public void MarkDropped_WithoutCurrentAttribute_ThrowsInvalidOperationException(string state)
    {
        var limiter = CreateLimiter();
        using var activity = CreateActivity("a");

        Assert.Throws<InvalidOperationException>(() =>
        {
            using var limits = limiter.BeginBatch();
            var attributes = limits.CreateAttributeReader(activity);
            if (state != "BeforeRead")
            {
                Assert.True(attributes.TryRead(out _), $"Expected the single attribute to be available before testing the '{state}' reader state.");
                if (state == "AfterMarkDropped")
                {
                    attributes.MarkDropped();
                }
                else if (state == "AfterCompletion")
                {
                    Assert.False(attributes.TryRead(out _), "The single attribute has already been read; the next read should complete the reader.");
                }
                else
                {
                    Assert.Equal(0, attributes.DroppedCount);
                }
            }

            attributes.MarkDropped();
        });
    }

    [Fact]
    public void TryRead_ZeroCountLimit_DropsAttributesButResolvesStatus()
    {
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 0);
        using var activity = CreateActivity("a", "b");
        activity.SetTag("otel.status_code", "ERROR");

        using var limits = limiter.BeginBatch();
        var attributes = limits.CreateAttributeReader(activity);

        Assert.Empty(ReadKeys(ref attributes));
        Assert.Equal(2, attributes.DroppedCount);
        Assert.Equal(ActivityStatusCode.Error, attributes.Status);
    }

    [Fact]
    public void DroppedCount_PartialRead_CountsRemainingAttributesAndResolvesStatus()
    {
        var limiter = CreateLimiter();
        using var activity = CreateActivity("a", "b", "c", "d", "e");
        activity.SetTag("otel.status_code", "ERROR");
        activity.SetTag("otel.status_description", "failed");

        using var limits = limiter.BeginBatch();
        var attributes = limits.CreateAttributeReader(activity);

        Assert.True(attributes.TryRead(out _), "Expected to read the first attribute successfully.");
        Assert.True(attributes.TryRead(out _), "Expected to read the second attribute successfully.");

        Assert.Equal(3, attributes.DroppedCount);
        Assert.Equal(ActivityStatusCode.Error, attributes.Status);
        Assert.Equal("failed", attributes.StatusDescription);
        Assert.False(attributes.TryRead(out _), "Expected no more attributes to be read.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Status_ReadBeforeAttributes_DoesNotFinishReader(int attributesReadFirst)
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter();
        using var activity = CreateActivity("a", "b", "c");
        activity.SetTag("otel.status_code", "ERROR");
        activity.SetTag("otel.status_description", "failed");

        using (var limits = limiter.BeginBatch())
        {
            var attributes = limits.CreateAttributeReader(activity);
            var keys = new List<string>();
            for (var i = 0; i < attributesReadFirst; i++)
            {
                Assert.True(attributes.TryRead(out var attribute), "Expected to read an attribute before the status.");
                keys.Add(attribute.Key);
            }

            Assert.Equal(ActivityStatusCode.Error, attributes.Status);
            Assert.Equal("failed", attributes.StatusDescription);

            keys.AddRange(ReadKeys(ref attributes));
            Assert.Equal(["a", "b", "c"], keys);
            Assert.Equal(0, attributes.DroppedCount);
            Assert.Equal(ActivityStatusCode.Error, attributes.Status);
            Assert.Equal("failed", attributes.StatusDescription);
        }

        Assert.Empty(GetWarnings(listener));
    }

    [Fact]
    public void DroppedCount_ReadRepeatedly_RecordsDropsOnce()
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter(o => o.AttributeCountLimit = 1);
        using var activity = CreateActivity("a", "b", "c");

        using (var limits = limiter.BeginBatch())
        {
            var attributes = limits.CreateAttributeReader(activity);
            _ = ReadKeys(ref attributes);
            Assert.Equal(2, attributes.DroppedCount);
            Assert.Equal(2, attributes.DroppedCount);
            Assert.False(attributes.TryRead(out _), "A completed attribute reader should remain exhausted after repeated DroppedCount reads.");
        }

        var warning = Assert.Single(GetWarnings(listener));
        Assert.Equal(1, warning.SpanCount);
        Assert.Equal(2, warning.DroppedAttributeCount);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(2, 0)]
    [InlineData(2, 2)]
    [InlineData(int.MaxValue, 2)]
    public void TryRead_AttributesWithinCountLimit_ReturnsAllAttributesWithoutDrops(int limit, int attributeCount)
    {
        using var listener = ListenForWarnings();
        var limiter = CreateLimiter(o => o.AttributeCountLimit = limit);
        var keys = Enumerable.Range(0, attributeCount).Select(i => $"key{i}").ToArray();
        using var activity = CreateActivity(keys);

        using (var limits = limiter.BeginBatch())
        {
            var attributes = limits.CreateAttributeReader(activity);
            Assert.Equal(keys, ReadKeys(ref attributes));
            Assert.Equal(0, attributes.DroppedCount);
            Assert.Equal(ActivityStatusCode.Unset, attributes.Status);
            Assert.Null(attributes.StatusDescription);
            Assert.False(attributes.TryRead(out _), $"The reader should remain exhausted after reading all {attributeCount} attributes with a count limit of {limit}.");
        }

        Assert.Empty(GetWarnings(listener));
    }
}
