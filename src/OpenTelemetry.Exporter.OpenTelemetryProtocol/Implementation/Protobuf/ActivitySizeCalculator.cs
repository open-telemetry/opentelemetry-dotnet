// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

internal class ActivitySizeCalculator
{
    internal const int TraceIdSize = 16;
    internal const int SpanIdSize = 8;
    private const int KindSize = 1;
    private const int TimeSize = 8;
    private const int I32Size = 4;

    private readonly SdkLimitOptions sdkLimitOptions;

    internal ActivitySizeCalculator(SdkLimitOptions sdkLimitOptions)
    {
        this.sdkLimitOptions = sdkLimitOptions;
    }

    internal static int ComputeActivityStatusSize(Activity activity, StatusCode? statusCode, string? statusMessage)
    {
        int size = 0;
        if (activity.Status == ActivityStatusCode.Unset && statusCode == null)
        {
            return size;
        }

        if (activity.Status != ActivityStatusCode.Unset)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Status_code);
            size += 1;

            if (activity.Status == ActivityStatusCode.Error && activity.StatusDescription != null)
            {
                size += CommonTypesSizeCalculator.ComputeStringWithTagSize(FieldNumberConstants.Status_message, activity.StatusDescription);
            }
        }
        else if (statusCode != StatusCode.Unset)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Status_code);
            size += 1;

            if (statusCode == StatusCode.Error && statusMessage != null)
            {
                size += CommonTypesSizeCalculator.ComputeStringWithTagSize(FieldNumberConstants.Status_message, statusMessage);
            }
        }

        return size;
    }

    internal int ComputeScopeSpanSize(string activitySourceName, string? activitySourceVersion, List<Activity> scopeActivities)
    {
        int size = 0;
        var instrumentationScopeSize = CommonTypesSizeCalculator.ComputeInstrumentationScopeSize(activitySourceName, activitySourceVersion);
        size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.ScopeSpans_scope, instrumentationScopeSize);

        foreach (var activity in scopeActivities)
        {
            var activitySize = this.ComputeActivitySize(activity);
            size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.ScopeSpans_span, activitySize);
        }

        return size;
    }

    internal int ComputeActivitySize(Activity activity)
    {
        int size = 0;
        size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.Span_trace_id, TraceIdSize);
        size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.Span_span_id, SpanIdSize);

        if (activity.ParentSpanId != default)
        {
            size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.Span_parent_span_id, SpanIdSize);
        }

        size += CommonTypesSizeCalculator.ComputeStringWithTagSize(FieldNumberConstants.Span_name, activity.DisplayName);

        if (activity.TraceStateString != null)
        {
            size += CommonTypesSizeCalculator.ComputeStringWithTagSize(FieldNumberConstants.Span_trace_state, activity.TraceStateString);
        }

        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_kind);
        size += KindSize; // kind value
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_start_time_unix_nano);
        size += TimeSize; // start time
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_end_time_unix_nano);
        size += TimeSize; // end time

        size += this.ComputeActivityAttributesSize(activity, out var droppedCount, out var statusCode, out var statusMessage);
        if (droppedCount > 0)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_dropped_attributes_count);
            size += WireTypesSizeCalculator.ComputeRawVarint32Size((uint)droppedCount);
        }

        var statusMessageSize = ComputeActivityStatusSize(activity, statusCode, statusMessage);
        if (statusMessageSize > 0)
        {
            size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.Span_status, statusMessageSize);
        }

        size += this.ComputeActivityEventsSize(activity, out var droppedEventCount);
        if (droppedEventCount > 0)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_dropped_events_count);
            size += WireTypesSizeCalculator.ComputeRawVarint32Size((uint)droppedEventCount);
        }

        size += this.ComputeActivityLinksSize(activity, out var droppedLinkCount);
        if (droppedLinkCount > 0)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_dropped_links_count);
            size += WireTypesSizeCalculator.ComputeRawVarint32Size((uint)droppedLinkCount);
        }

        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_flags);
        size += I32Size;

        return size;
    }

    internal int ComputeActivityLinksSize(Activity activity, out int droppedLinkCount)
    {
        droppedLinkCount = 0;
        int size = 0;
        int maxLinksCount = this.sdkLimitOptions.SpanLinkCountLimit ?? int.MaxValue;
        int linkCount = 0;
        if (activity.Links != null)
        {
            foreach (ref readonly var link in activity.EnumerateLinks())
            {
                if (linkCount < maxLinksCount)
                {
                    var linkSize = this.ComputeActivityLinkSize(link);
                    size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.Span_links, linkSize);
                    linkCount++;
                }
                else
                {
                    droppedLinkCount++;
                }
            }
        }

        return size;
    }

    internal int ComputeActivityLinkSize(ActivityLink link)
    {
        int size = 0;
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Link_trace_id);
        size += WireTypesSizeCalculator.ComputeLengthSize(16);
        size += TraceIdSize;
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Link_span_id);
        size += WireTypesSizeCalculator.ComputeLengthSize(8);
        size += SpanIdSize;

        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Link_flags);
        size += I32Size;

        int droppedAttributeCount = 0;
        int attributeCount = 0;
        int maxAttributeValueLength = this.sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;

        foreach (ref readonly var tag in link.EnumerateTagObjects())
        {
            if (attributeCount < this.sdkLimitOptions.SpanLinkAttributeCountLimit)
            {
                var keyValueSize = CommonTypesSizeCalculator.ComputeKeyValuePairSize(tag, maxAttributeValueLength);
                size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.Link_attributes, keyValueSize);
                attributeCount++;
            }
            else
            {
                droppedAttributeCount++;
            }
        }

        if (droppedAttributeCount > 0)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Link_dropped_attributes_count);
            size += WireTypesSizeCalculator.ComputeLengthSize(droppedAttributeCount);
        }

        return size;
    }

    internal int ComputeActivityEventsSize(Activity activity, out int droppedEventCount)
    {
        droppedEventCount = 0;
        int size = 0;
        int maxEventCountLimit = this.sdkLimitOptions.SpanEventCountLimit ?? int.MaxValue;
        int eventCount = 0;
        if (activity.Events != null)
        {
            foreach (ref readonly var evnt in activity.EnumerateEvents())
            {
                if (eventCount < maxEventCountLimit)
                {
                    var evntSize = this.ComputeActivityEventSize(evnt);
                    size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.Span_events, evntSize);
                    eventCount++;
                }
                else
                {
                    droppedEventCount++;
                }
            }
        }

        return size;
    }

    internal int ComputeActivityEventSize(ActivityEvent evnt)
    {
        int spanEventAttributeCountLimit = this.sdkLimitOptions.SpanEventAttributeCountLimit ?? int.MaxValue;
        int maxAttributeValueLength = this.sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
        int droppedAttributeCount = 0;
        int attributeCount = 0;
        int size = 0;
        size += CommonTypesSizeCalculator.ComputeStringWithTagSize(FieldNumberConstants.Event_name, evnt.Name);
        size += TimeSize; // event time
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Event_time_unix_nano);
        foreach (ref readonly var tag in evnt.EnumerateTagObjects())
        {
            if (attributeCount < spanEventAttributeCountLimit)
            {
                var keyValueSize = CommonTypesSizeCalculator.ComputeKeyValuePairSize(tag, maxAttributeValueLength);
                size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.Event_attributes, keyValueSize);
                attributeCount++;
            }
            else
            {
                droppedAttributeCount++;
            }
        }

        if (droppedAttributeCount > 0)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Event_dropped_attributes_count);
            size += WireTypesSizeCalculator.ComputeLengthSize(droppedAttributeCount);
        }

        return size;
    }

    internal int ComputeActivityAttributesSize(Activity activity, out int droppedCount, out StatusCode? statusCode, out string? statusMessage)
    {
        statusCode = null;
        statusMessage = null;
        int maxAttributeCount = this.sdkLimitOptions.SpanAttributeCountLimit ?? int.MaxValue;
        int maxAttributeValueLength = this.sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
        int size = 0;
        int attributeCount = 0;
        droppedCount = 0;
        foreach (ref readonly var tag in activity.EnumerateTagObjects())
        {
            switch (tag.Key)
            {
                case SpanAttributeConstants.StatusCodeKey:
                    statusCode = StatusHelper.GetStatusCodeForTagValue(tag.Value as string);
                    continue;
                case SpanAttributeConstants.StatusDescriptionKey:
                    statusMessage = tag.Value as string;
                    continue;
            }

            if (attributeCount < maxAttributeCount)
            {
                var keyValueSize = CommonTypesSizeCalculator.ComputeKeyValuePairSize(tag, maxAttributeValueLength);
                size += CommonTypesSizeCalculator.ComputeSizeWithTagAndLengthPrefix(FieldNumberConstants.Span_attributes, keyValueSize);
                attributeCount++;
            }
            else
            {
                droppedCount++;
            }
        }

        return size;
    }
}
