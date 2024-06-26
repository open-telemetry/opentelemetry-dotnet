// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Collections.Concurrent;
using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Custom.Serializer;

internal class ActivitySerializer
{
    private static readonly ConcurrentBag<List<Activity>> ActivityListPool = new();

    private readonly SdkLimitOptions sdkLimitOptions;

    private readonly ActivitySizeCalculator activitySizeCalculator;

    internal ActivitySerializer(SdkLimitOptions sdkLimitOptions)
    {
        this.sdkLimitOptions = sdkLimitOptions;
        this.activitySizeCalculator = new ActivitySizeCalculator(sdkLimitOptions);
    }

    internal int Serialize(ref byte[] buffer, int offset, Resource resource, Batch<Activity> batch)
    {
        Dictionary<string, List<Activity>> scopeTraces = new();
        foreach (var activity in batch)
        {
            if (scopeTraces.TryGetValue(activity.Source.Name, out var activityList))
            {
                activityList.Add(activity);
            }
            else
            {
                if (!ActivityListPool.TryTake(out var newList))
                {
                    newList = new List<Activity>();
                }

                newList.Add(activity);
                scopeTraces[activity.Source.Name] = newList;
            }
        }

        var cursor = this.SerializeResourceSpans(ref buffer, offset, resource, scopeTraces);

        this.ReturnActivityListToPool(scopeTraces);

        return cursor;
    }

    internal void ReturnActivityListToPool(Dictionary<string, List<Activity>>? scopeTraces)
    {
        if (scopeTraces != null)
        {
            foreach (var entry in scopeTraces)
            {
                entry.Value.Clear();
                ActivityListPool.Add(entry.Value);
            }
        }
    }

    private static int SerializeTraceId(ref byte[] buffer, int cursor, ActivityTraceId activityTraceId)
    {
        // TODO: optimize alloc for buffer resizing scenario.
        if (cursor + ActivitySizeCalculator.TraceIdSize <= buffer.Length)
        {
            var traceBytes = new Span<byte>(buffer, cursor, ActivitySizeCalculator.TraceIdSize);
            activityTraceId.CopyTo(traceBytes);
            cursor += ActivitySizeCalculator.TraceIdSize;

            return cursor;
        }
        else
        {
            var traceIdBytes = new byte[ActivitySizeCalculator.TraceIdSize];
            activityTraceId.CopyTo(traceIdBytes);

            foreach (var b in traceIdBytes)
            {
                cursor = Writer.WriteSingleByte(ref buffer, cursor, b);
            }

            return cursor;
        }
    }

    private static int SerializeSpanId(ref byte[] buffer, int cursor, ActivitySpanId activitySpanId)
    {
        // TODO: optimize alloc for buffer resizing scenario.
        if (cursor + ActivitySizeCalculator.SpanIdSize <= buffer.Length)
        {
            var spanIdBytes = new Span<byte>(buffer, cursor, ActivitySizeCalculator.SpanIdSize);
            activitySpanId.CopyTo(spanIdBytes);
            cursor += ActivitySizeCalculator.SpanIdSize;

            return cursor;
        }
        else
        {
            var spanIdBytes = new byte[ActivitySizeCalculator.SpanIdSize];
            activitySpanId.CopyTo(spanIdBytes);

            foreach (var b in spanIdBytes)
            {
                cursor = Writer.WriteSingleByte(ref buffer, cursor, b);
            }

            return cursor;
        }
    }

    private static int SerializeTraceFlags(ref byte[] buffer, int cursor, ActivityTraceFlags activityTraceFlags, bool hasRemoteParent, int fieldNumber)
    {
        uint spanFlags = (uint)activityTraceFlags & (byte)0x000000FF;

        spanFlags |= 0x00000100;
        if (hasRemoteParent)
        {
            spanFlags |= 0x00000200;
        }

        cursor = Writer.WriteFixed32WithTag(ref buffer, cursor, fieldNumber, spanFlags);

        return cursor;
    }

    private static int SerializeActivityStatus(ref byte[] buffer, int cursor, Activity activity, StatusCode? statusCode, string? statusMessage)
    {
        if (activity.Status == ActivityStatusCode.Unset && statusCode == null)
        {
            return cursor;
        }

        var statusSize = ActivitySizeCalculator.ComputeActivityStatusSize(activity, statusCode, statusMessage);

        if (statusSize > 0)
        {
            cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, statusSize, FieldNumberConstants.Span_status, WireType.LEN);
        }

        if (activity.Status != ActivityStatusCode.Unset)
        {
            cursor = Writer.WriteEnumWithTag(ref buffer, cursor, FieldNumberConstants.Status_code, (int)activity.Status);

            if (activity.Status == ActivityStatusCode.Error && activity.StatusDescription != null)
            {
                cursor = Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.Status_message, activity.StatusDescription);
            }
        }
        else if (statusCode != StatusCode.Unset)
        {
            cursor = Writer.WriteEnumWithTag(ref buffer, cursor, FieldNumberConstants.Status_code, (int)statusCode!);

            if (statusCode == StatusCode.Error && statusMessage != null)
            {
                cursor = Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.Status_message, statusMessage);
            }
        }

        return cursor;
    }

    // SerializeResourceSpans
    private int SerializeResourceSpans(ref byte[] buffer, int cursor, Resource resource, Dictionary<string, List<Activity>> scopeTraces)
    {
        var start = cursor;

        int maxAttributeValueLength = this.sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;

        var resourceSpansSize = this.activitySizeCalculator.ComputeResourceSpansSize(resource, scopeTraces);
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, resourceSpansSize, FieldNumberConstants.ResourceSpans_resource, WireType.LEN);
        cursor = CommonTypesSerializer.SerializeResource(ref buffer, cursor, resource, maxAttributeValueLength);
        cursor = this.SerializeScopeSpans(ref buffer, cursor, scopeTraces);

        return cursor;
    }

    private int SerializeScopeSpans(ref byte[] buffer, int cursor, Dictionary<string, List<Activity>> scopeTraces)
    {
        if (scopeTraces != null)
        {
            foreach (KeyValuePair<string, List<Activity>> entry in scopeTraces)
            {
                var scopeSize = this.activitySizeCalculator.ComputeScopeSpanSize(entry.Key, entry.Value[0].Source.Version, entry.Value);
                cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, scopeSize, FieldNumberConstants.ResourceSpans_scope_spans, WireType.LEN);
                cursor = this.SerializeSingleScopeSpan(ref buffer, cursor, entry.Key, entry.Value[0].Source.Version, entry.Value);
            }
        }

        return cursor;
    }

    private int SerializeSingleScopeSpan(ref byte[] buffer, int cursor, string activitySourceName, string? activitySourceVersion, List<Activity> activities)
    {
        var instrumentationScopeSize = CommonTypesSizeCalculator.ComputeInstrumentationScopeSize(activitySourceName, activitySourceVersion);
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, instrumentationScopeSize, FieldNumberConstants.ScopeSpans_scope, WireType.LEN);
        cursor = Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.InstrumentationScope_name, activitySourceName);
        if (activitySourceVersion != null)
        {
            cursor = Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.InstrumentationScope_version, activitySourceVersion);
        }

        foreach (var activity in activities)
        {
            int spanSize = this.activitySizeCalculator.ComputeActivitySize(activity);
            cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, spanSize, FieldNumberConstants.ScopeSpans_span, WireType.LEN);
            cursor = this.SerializeActivity(ref buffer, cursor, activity);
        }

        return cursor;
    }

    private int SerializeActivity(ref byte[] buffer, int cursor, Activity activity)
    {
        cursor = Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.Span_name, activity.DisplayName);
        if (activity.TraceStateString != null)
        {
            cursor = Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.Span_trace_state, activity.TraceStateString);
        }

        cursor = Writer.WriteEnumWithTag(ref buffer, cursor, FieldNumberConstants.Span_kind, (int)activity.Kind + 1);
        cursor = Writer.WriteFixed64WithTag(ref buffer, cursor, FieldNumberConstants.Span_start_time_unix_nano, (ulong)activity.StartTimeUtc.ToUnixTimeNanoseconds());
        cursor = Writer.WriteFixed64WithTag(ref buffer, cursor, FieldNumberConstants.Span_end_time_unix_nano, (ulong)(activity.StartTimeUtc.ToUnixTimeNanoseconds() + activity.Duration.ToNanoseconds()));
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, 16, FieldNumberConstants.Span_trace_id, WireType.LEN);
        cursor = SerializeTraceId(ref buffer, cursor, activity.TraceId);
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, 8, FieldNumberConstants.Span_span_id, WireType.LEN);
        cursor = SerializeSpanId(ref buffer, cursor, activity.SpanId);
        if (activity.ParentSpanId != default)
        {
            cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, 8, FieldNumberConstants.Span_parent_span_id, WireType.LEN);
            cursor = SerializeSpanId(ref buffer, cursor, activity.ParentSpanId);
        }

        cursor = this.SerializeActivityTags(ref buffer, cursor, activity, out var statusCode, out var statusMessage);
        cursor = SerializeActivityStatus(ref buffer, cursor, activity, statusCode, statusMessage);
        cursor = this.SerializeActivityEvents(ref buffer, cursor, activity);
        cursor = this.SerializeActivityLinks(ref buffer, cursor, activity);
        cursor = SerializeTraceFlags(ref buffer, cursor, activity.ActivityTraceFlags, activity.HasRemoteParent, FieldNumberConstants.Span_flags);
        return cursor;
    }

    private int SerializeActivityTags(ref byte[] buffer, int cursor, Activity activity, out StatusCode? statusCode, out string? statusMessage)
    {
        statusCode = null;
        statusMessage = null;
        int maxAttributeCount = this.sdkLimitOptions.SpanAttributeCountLimit ?? int.MaxValue;
        int maxAttributeValueLength = this.sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
        int attributeCount = 0;
        int droppedAttributeCount = 0;
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
                cursor = CommonTypesSerializer.SerializeKeyValuePair(ref buffer, cursor, FieldNumberConstants.Span_attributes, tag, maxAttributeValueLength);
                attributeCount++;
            }
            else
            {
                droppedAttributeCount++;
            }
        }

        if (droppedAttributeCount > 0)
        {
            cursor = Writer.WriteTag(ref buffer, cursor, FieldNumberConstants.Span_dropped_attributes_count, WireType.VARINT);
            cursor = Writer.WriteVarint32(ref buffer, cursor, (uint)droppedAttributeCount);
        }

        return cursor;
    }

    private int SerializeActivityLinks(ref byte[] buffer, int cursor, Activity activity)
    {
        int maxLinksCount = this.sdkLimitOptions.SpanLinkCountLimit ?? int.MaxValue;
        int linkCount = 0;
        int droppedLinkCount = 0;

        foreach (ref readonly var link in activity.EnumerateLinks())
        {
            if (linkCount < maxLinksCount)
            {
                var linkSize = this.activitySizeCalculator.ComputeActivityLinkSize(link);
                cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, linkSize, FieldNumberConstants.Span_links, WireType.LEN);
                cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, ActivitySizeCalculator.TraceIdSize, FieldNumberConstants.Link_trace_id, WireType.LEN);
                cursor = SerializeTraceId(ref buffer, cursor, link.Context.TraceId);
                cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, ActivitySizeCalculator.SpanIdSize, FieldNumberConstants.Link_span_id, WireType.LEN);
                cursor = SerializeSpanId(ref buffer, cursor, link.Context.SpanId);
                cursor = SerializeTraceFlags(ref buffer, cursor, link.Context.TraceFlags, link.Context.IsRemote, FieldNumberConstants.Link_flags);
                cursor = this.SerializeLinkTags(ref buffer, cursor, link);
                linkCount++;
            }
            else
            {
                droppedLinkCount++;
            }
        }

        if (droppedLinkCount > 0)
        {
            cursor = Writer.WriteTag(ref buffer, cursor, FieldNumberConstants.Span_dropped_links_count, WireType.VARINT);
            cursor = Writer.WriteVarint32(ref buffer, cursor, (uint)droppedLinkCount);
        }

        return cursor;
    }

    private int SerializeLinkTags(ref byte[] buffer, int cursor, ActivityLink link)
    {
        int maxAttributeCount = this.sdkLimitOptions.SpanLinkAttributeCountLimit ?? int.MaxValue;
        int maxAttributeValueLength = this.sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
        int attributeCount = 0;
        int droppedAttributeCount = 0;
        foreach (ref readonly var tag in link.EnumerateTagObjects())
        {
            if (attributeCount < maxAttributeCount)
            {
                cursor = CommonTypesSerializer.SerializeKeyValuePair(ref buffer, cursor, FieldNumberConstants.Link_attributes, tag, maxAttributeValueLength);
                attributeCount++;
            }
            else
            {
                droppedAttributeCount++;
            }
        }

        if (droppedAttributeCount > 0)
        {
            cursor = Writer.WriteTag(ref buffer, cursor, FieldNumberConstants.Link_dropped_attributes_count, WireType.VARINT);
            cursor = Writer.WriteVarint32(ref buffer, cursor, (uint)droppedAttributeCount);
        }

        return cursor;
    }

    private int SerializeActivityEvents(ref byte[] buffer, int cursor, Activity activity)
    {
        int maxEventCountLimit = this.sdkLimitOptions.SpanEventCountLimit ?? int.MaxValue;
        int eventCount = 0;
        int droppedEventCount = 0;
        foreach (ref readonly var evnt in activity.EnumerateEvents())
        {
            if (eventCount < maxEventCountLimit)
            {
                int eventSize = this.activitySizeCalculator.ComputeActivityEventSize(evnt);
                cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, eventSize, FieldNumberConstants.Span_events, WireType.LEN);
                cursor = Writer.WriteStringWithTag(ref buffer, cursor, FieldNumberConstants.Event_name, evnt.Name);
                cursor = Writer.WriteFixed64WithTag(ref buffer, cursor, FieldNumberConstants.Event_time_unix_nano, (ulong)evnt.Timestamp.ToUnixTimeNanoseconds());
                cursor = this.SerializeEventTags(ref buffer, cursor, evnt);
                eventCount++;
            }
            else
            {
                droppedEventCount++;
            }
        }

        if (droppedEventCount > 0)
        {
            cursor = Writer.WriteTag(ref buffer, cursor, FieldNumberConstants.Span_dropped_events_count, WireType.VARINT);
            cursor = Writer.WriteVarint32(ref buffer, cursor, (uint)droppedEventCount);
        }

        return cursor;
    }

    private int SerializeEventTags(ref byte[] buffer, int cursor, ActivityEvent evnt)
    {
        int maxAttributeCount = this.sdkLimitOptions.SpanEventAttributeCountLimit ?? int.MaxValue;
        int maxAttributeValueLength = this.sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
        int attributeCount = 0;
        int droppedAttributeCount = 0;
        foreach (ref readonly var tag in evnt.EnumerateTagObjects())
        {
            if (attributeCount < maxAttributeCount)
            {
                cursor = CommonTypesSerializer.SerializeKeyValuePair(ref buffer, cursor, FieldNumberConstants.Event_attributes, tag, maxAttributeValueLength);
                attributeCount++;
            }
            else
            {
                droppedAttributeCount++;
            }
        }

        if (droppedAttributeCount > 0)
        {
            cursor = Writer.WriteTag(ref buffer, cursor, FieldNumberConstants.Event_dropped_attributes_count, WireType.VARINT);
            cursor = Writer.WriteVarint32(ref buffer, cursor, (uint)droppedAttributeCount);
        }

        return cursor;
    }
}
