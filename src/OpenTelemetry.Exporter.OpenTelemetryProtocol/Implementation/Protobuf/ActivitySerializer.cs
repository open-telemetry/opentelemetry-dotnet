// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using System.Globalization;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

internal class ActivitySerializer
{
    private readonly SdkLimitOptions sdkLimitOptions;

    private readonly ActivitySizeCalculator activitySizeCalculator;

    private readonly Dictionary<string, List<Activity>> scopeTraces = new();

    internal ActivitySerializer(SdkLimitOptions sdkLimitOptions)
    {
        this.sdkLimitOptions = sdkLimitOptions;
        this.activitySizeCalculator = new ActivitySizeCalculator(sdkLimitOptions);
    }

    internal int Serialize(ref byte[] buffer, int offset, Resource resource, Batch<Activity> batch)
    {
        foreach (var activity in batch)
        {
            if (this.scopeTraces.TryGetValue(activity.Source.Name, out var activityList))
            {
                activityList.Add(activity);
            }
            else
            {
                var newList = new List<Activity>() { activity };
                this.scopeTraces[activity.Source.Name] = newList;
            }
        }

        var cursor = this.SerializeResourceSpans(ref buffer, offset, resource);

        this.ClearScopeTraces();

        return cursor;
    }

    internal void ClearScopeTraces()
    {
        foreach (var entry in this.scopeTraces)
        {
            entry.Value.Clear();
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

    private int SerializeLinkTags(ref byte[] buffer, int cursor, ActivityLink link)
    {
        int maxAttributeCount = this.sdkLimitOptions.SpanLinkAttributeCountLimit ?? int.MaxValue;
        int attributeCount = 0;
        int droppedAttributeCount = 0;
        foreach (var tag in link.EnumerateTagObjects())
        {
            if (attributeCount < maxAttributeCount)
            {
                cursor = this.SerializeKeyValuePair(ref buffer, cursor, FieldNumberConstants.Link_attributes, tag);
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

    private int SerializeKeyValuePair(ref byte[] buffer, int cursor, int fieldNumber, KeyValuePair<string, object?> tag)
    {
        var tagSize = this.activitySizeCalculator.ComputeKeyValuePairSize(tag);
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, tagSize, fieldNumber, WireType.LEN);
        cursor = Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.KeyValue_key, tag.Key);
        cursor = this.SerializeAnyValue(ref buffer, cursor, tag.Value, FieldNumberConstants.KeyValue_value);

        return cursor;
    }

    private int SerializeEventTags(ref byte[] buffer, int cursor, ActivityEvent evnt)
    {
        int maxAttributeCount = this.sdkLimitOptions.SpanEventAttributeCountLimit ?? int.MaxValue;
        int attributeCount = 0;
        int droppedAttributeCount = 0;
        foreach (var tag in evnt.EnumerateTagObjects())
        {
            if (attributeCount < maxAttributeCount)
            {
                cursor = this.SerializeKeyValuePair(ref buffer, cursor, FieldNumberConstants.Event_attributes, tag);
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

    private int SerializeActivityTags(ref byte[] buffer, int cursor, Activity activity, out StatusCode? statusCode, out string? statusMessage)
    {
        statusCode = null;
        statusMessage = null;
        int maxAttributeCount = this.sdkLimitOptions.SpanAttributeCountLimit ?? int.MaxValue;
        int attributeCount = 0;
        int droppedAttributeCount = 0;
        foreach (var tag in activity.EnumerateTagObjects())
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
                cursor = this.SerializeKeyValuePair(ref buffer, cursor, FieldNumberConstants.Span_attributes, tag);
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

    private int SerializeArray(ref byte[] buffer, int cursor, Array array)
    {
        var arraySize = this.activitySizeCalculator.ComputeArrayValueSize(array);
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, arraySize, FieldNumberConstants.AnyValue_array_value, WireType.LEN);
        foreach (var ar in array)
        {
            cursor = this.SerializeAnyValue(ref buffer, cursor, ar, FieldNumberConstants.ArrayValue_Value);
        }

        return cursor;
    }

    private int SerializeAnyValue(ref byte[] buffer, int cursor, object? value, int fieldNumber)
    {
        var anyValueSize = this.activitySizeCalculator.ComputeAnyValueSize(value);
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, anyValueSize, fieldNumber, WireType.LEN);
        if (value == null)
        {
            // cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, 0, 0, WireType.LEN);
            return cursor;
        }

        var stringSizeLimit = this.sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
        switch (value)
        {
            case char:
            case string:
                var rawStringVal = Convert.ToString(value, CultureInfo.InvariantCulture);
                var stringVal = rawStringVal;
                if (rawStringVal?.Length > stringSizeLimit)
                {
                    stringVal = rawStringVal.Substring(0, stringSizeLimit);
                }

                return Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.AnyValue_string_value, stringVal);
            case bool b:
                return Writer.WriteBoolWithTag(ref buffer, cursor, FieldNumberConstants.AnyValue_bool_value, (bool)value);
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
            case ulong:
                return Writer.WriteInt64WithTag(ref buffer, cursor, FieldNumberConstants.AnyValue_int_value, (ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture));
            case float:
            case double:
                return Writer.WriteDoubleWithTag(ref buffer, cursor, FieldNumberConstants.AnyValue_double_value, Convert.ToDouble(value, CultureInfo.InvariantCulture));
            case Array array:
                return this.SerializeArray(ref buffer, cursor, array);
            default:
                var defaultRawStringVal = Convert.ToString(value); // , CultureInfo.InvariantCulture);
                var defaultStringVal = defaultRawStringVal;
                if (defaultRawStringVal?.Length > stringSizeLimit)
                {
                    defaultStringVal = defaultRawStringVal.Substring(0, stringSizeLimit);
                }

                return Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.AnyValue_string_value, defaultStringVal);
        }
    }

    // SerializeResourceSpans
    private int SerializeResourceSpans(ref byte[] buffer, int cursor, Resource resource)
    {
        var start = cursor;

        // Leave 4 bytes for length.
        cursor += 4;
        var valueStart = cursor;

        cursor = this.SerializeResource(ref buffer, cursor, resource);
        cursor = this.SerializeScopeSpans(ref buffer, cursor);
        start = Writer.WriteTag(ref buffer, start, FieldNumberConstants.ResourceSpans_resource, WireType.LEN);
        _ = Writer.WriteLengthCustom(ref buffer, start, cursor - valueStart);

        return cursor;
    }

    private int SerializeResource(ref byte[] buffer, int cursor, Resource resource)
    {
        if (resource != Resource.Empty)
        {
            var resourceSize = this.activitySizeCalculator.ComputeResourceSize(resource);
            if (resourceSize > 0)
            {
                cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, resourceSize, FieldNumberConstants.ResourceSpans_resource, WireType.LEN);
                foreach (var attribute in resource.Attributes)
                {
                    var tagSize = this.activitySizeCalculator.ComputeKeyValuePairSize(attribute!);
                    cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, tagSize, FieldNumberConstants.Resource_attributes, WireType.LEN);
                    cursor = Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.KeyValue_key, attribute.Key);
                    cursor = this.SerializeAnyValue(ref buffer, cursor, attribute.Value, FieldNumberConstants.KeyValue_value);
                }
            }
        }

        return cursor;
    }

    // SerializeScopeSpans
    private int SerializeScopeSpans(ref byte[] buffer, int cursor)
    {
        foreach (KeyValuePair<string, List<Activity>> entry in this.scopeTraces)
        {
            var scopeSize = this.activitySizeCalculator.ComputeScopeSize(entry.Key, entry.Value[0].Source.Version, entry.Value);
            cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, scopeSize, FieldNumberConstants.ResourceSpans_scope_spans, WireType.LEN);
            cursor = this.SerializeSingleScopeSpan(ref buffer, cursor, entry.Key, entry.Value[0].Source.Version, entry.Value);
        }

        return cursor;
    }

    // SerializeSingleScopeSpan
    private int SerializeSingleScopeSpan(ref byte[] buffer, int cursor, string activitySourceName, string? activitySourceVersion, List<Activity> activities)
    {
        var instrumentationScopeSize = this.activitySizeCalculator.ComputeInstrumentationScopeSize(activitySourceName, activitySourceVersion);
        cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, instrumentationScopeSize, FieldNumberConstants.ScopeSpans_scope, WireType.LEN);
        cursor = Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.InstrumentationScope_name, activitySourceName);
        if (activitySourceVersion != null)
        {
            cursor = Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.InstrumentationScope_version, activitySourceVersion);
        }

        foreach (var activity in activities)
        {
            int spanSize = this.activitySizeCalculator.ComputeActivitySize(activity);
            cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, spanSize, FieldNumberConstants.ScopeSpans_span, WireType.LEN);
            cursor = this.SerializeActivity(ref buffer, cursor, activity);
        }

        return cursor;
    }

    // Serialize Span
    private int SerializeActivity(ref byte[] buffer, int cursor, Activity activity)
    {
        cursor = Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.Span_name, activity.DisplayName);
        if (activity.TraceStateString != null)
        {
            cursor = Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.Span_trace_state, activity.TraceStateString);
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
        cursor = this.SerializeActivityStatus(ref buffer, cursor, activity, statusCode, statusMessage);
        cursor = this.SerializeActivityEvents(ref buffer, cursor, activity);
        cursor = this.SerializeActivityLinks(ref buffer, cursor, activity);
        cursor = SerializeTraceFlags(ref buffer, cursor, activity.ActivityTraceFlags, activity.HasRemoteParent, FieldNumberConstants.Span_flags);
        return cursor;
    }

    private int SerializeActivityStatus(ref byte[] buffer, int cursor, Activity activity, StatusCode? statusCode, string? statusMessage)
    {
        if (activity.Status == ActivityStatusCode.Unset && statusCode == null)
        {
            return cursor;
        }

        var statusSize = this.activitySizeCalculator.ComputeActivityStatusSize(activity, statusCode, statusMessage);

        if (statusSize > 0)
        {
            cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, statusSize, FieldNumberConstants.Span_status, WireType.LEN);
        }

        if (activity.Status != ActivityStatusCode.Unset)
        {
            cursor = Writer.WriteEnumWithTag(ref buffer, cursor, FieldNumberConstants.Status_code, (int)activity.Status);

            if (activity.Status == ActivityStatusCode.Error && activity.StatusDescription != null)
            {
                cursor = Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.Status_message, activity.StatusDescription);
            }
        }
        else if (statusCode != StatusCode.Unset)
        {
            cursor = Writer.WriteEnumWithTag(ref buffer, cursor, FieldNumberConstants.Status_code, (int)statusCode!);

            if (statusCode == StatusCode.Error && statusMessage != null)
            {
                cursor = Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.Status_message, statusMessage);
            }
        }

        return cursor;
    }

    private int SerializeActivityLinks(ref byte[] buffer, int cursor, Activity activity)
    {
        int droppedLinkCount = 0;
        int maxLinksCount = this.sdkLimitOptions.SpanLinkCountLimit ?? int.MaxValue;
        int linkCount = 0;

        foreach (var link in activity.EnumerateLinks())
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

    private int SerializeActivityEvents(ref byte[] buffer, int cursor, Activity activity)
    {
        int maxEventCountLimit = this.sdkLimitOptions.SpanEventCountLimit ?? int.MaxValue;
        int eventCount = 0;
        int droppedEventCount = 0;
        foreach (var evnt in activity.EnumerateEvents())
        {
            if (eventCount < maxEventCountLimit)
            {
                int eventSize = this.activitySizeCalculator.ComputeActivityEventSize(evnt);
                cursor = Writer.WriteTagAndLengthPrefix(ref buffer, cursor, eventSize, FieldNumberConstants.Span_events, WireType.LEN);
                cursor = Writer.WriteStringTag(ref buffer, cursor, FieldNumberConstants.Event_name, evnt.Name);
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
}
