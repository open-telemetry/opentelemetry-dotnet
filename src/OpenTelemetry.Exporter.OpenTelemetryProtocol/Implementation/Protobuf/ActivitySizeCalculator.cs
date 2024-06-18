// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

using System.Diagnostics;
using System.Globalization;
using System.Text;
using OpenTelemetry.Internal;
using OpenTelemetry.Resources;
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

    internal int ComputeActivitySize(Activity activity)
    {
        int size = 0;
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_trace_id);
        size += WireTypesSizeCalculator.ComputeLengthSize(TraceIdSize);
        size += TraceIdSize;
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_span_id);
        size += WireTypesSizeCalculator.ComputeLengthSize(SpanIdSize);
        size += SpanIdSize;
        if (activity.ParentSpanId != default)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_parent_span_id);
            size += WireTypesSizeCalculator.ComputeLengthSize(SpanIdSize);
            size += SpanIdSize;
        }

        size += ComputeStringWithTagSize(FieldNumberConstants.Span_name, activity.DisplayName);

        if (activity.TraceStateString != null)
        {
            size += ComputeStringWithTagSize(FieldNumberConstants.Span_trace_state, activity.TraceStateString);
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

        var statusMessageSize = this.ComputeActivityStatusSize(activity, statusCode, statusMessage);
        if (statusMessageSize > 0)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_status);
            size += WireTypesSizeCalculator.ComputeLengthSize(statusMessageSize);
            size += statusMessageSize;
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

    internal int ComputeActivityStatusSize(Activity activity, StatusCode? statusCode, string? statusMessage)
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
                size += ComputeStringWithTagSize(FieldNumberConstants.Status_message, activity.StatusDescription);
            }
        }
        else if (statusCode != StatusCode.Unset)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Status_code);
            size += 1;

            if (statusCode == StatusCode.Error && statusMessage != null)
            {
                size += ComputeStringWithTagSize(FieldNumberConstants.Status_message, statusMessage);
            }
        }

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
            foreach (var link in activity.EnumerateLinks())
            {
                if (linkCount < maxLinksCount)
                {
                    var linkSize = this.ComputeActivityLinkSize(link);
                    size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_links);
                    size += WireTypesSizeCalculator.ComputeLengthSize(linkSize);
                    size += linkSize;

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

        foreach (var tag in link.EnumerateTagObjects())
        {
            if (attributeCount < this.sdkLimitOptions.SpanLinkAttributeCountLimit)
            {
                var keyValueSize = this.ComputeKeyValuePairSize(tag);
                size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Link_attributes);
                size += WireTypesSizeCalculator.ComputeLengthSize(keyValueSize); // length prefix for key value pair.
                size += keyValueSize;
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
            foreach (var evnt in activity.EnumerateEvents())
            {
                if (eventCount < maxEventCountLimit)
                {
                    var evntSize = this.ComputeActivityEventSize(evnt);
                    size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_events);
                    size += WireTypesSizeCalculator.ComputeLengthSize(evntSize);
                    size += evntSize;
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
        int droppedAttributeCount = 0;
        int attributeCount = 0;
        int size = 0;
        size += ComputeStringWithTagSize(FieldNumberConstants.Event_name, evnt.Name);
        size += TimeSize; // event time
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Event_time_unix_nano);
        foreach (var tag in evnt.EnumerateTagObjects())
        {
            if (attributeCount < spanEventAttributeCountLimit)
            {
                var keyValueSize = this.ComputeKeyValuePairSize(tag);
                size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Event_attributes);
                size += WireTypesSizeCalculator.ComputeLengthSize(keyValueSize); // length prefix for key value pair.
                size += keyValueSize;
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
        droppedCount = 0;
        int size = 0;
        int attributeCount = 0;
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
                var keyValueSize = this.ComputeKeyValuePairSize(tag);
                size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_attributes);
                size += WireTypesSizeCalculator.ComputeLengthSize(keyValueSize); // length prefix for key value pair.
                size += keyValueSize;
                attributeCount++;
            }
            else
            {
                droppedCount++;
            }
        }

        return size;
    }

    internal int ComputeKeyValuePairSize(KeyValuePair<string, object?> tag)
    {
        int size = 0;
        size += ComputeStringWithTagSize(FieldNumberConstants.KeyValue_key, tag.Key);

        var anyValueSize = this.ComputeAnyValueSize(tag.Value!);
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.KeyValue_value);
        size += WireTypesSizeCalculator.ComputeLengthSize(anyValueSize); // length prefix for any value pair.
        size += anyValueSize;

        return size;
    }

    internal int ComputeAnyValueSize(object? value)
    {
        if (value == null)
        {
            return 0;
        }

        var stringSizeLimit = this.sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
        switch (value)
        {
            case char:
                return ComputeStringWithTagSize(FieldNumberConstants.AnyValue_string_value, Convert.ToString(value, CultureInfo.InvariantCulture)!);
            case string:
                var rawStringVal = Convert.ToString(value, CultureInfo.InvariantCulture);
                var stringVal = rawStringVal;
                if (rawStringVal?.Length > stringSizeLimit)
                {
                    stringVal = rawStringVal.Substring(0, stringSizeLimit);
                }

                return ComputeStringWithTagSize(FieldNumberConstants.AnyValue_string_value, stringVal!);
            case bool:
                return 1 + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_bool_value);
            case byte:
            case sbyte:
            case short:
            case ushort:
            case int:
            case uint:
            case long:
            case ulong:
                return WireTypesSizeCalculator.ComputeRawVarint64Size((ulong)Convert.ToInt64(value, CultureInfo.InvariantCulture)) + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_int_value);
            case float:
            case double:
                return 8 + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_double_value);
            case Array array:
                var arraySize = this.ComputeArrayValueSize(array);
                return WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_array_value) + WireTypesSizeCalculator.ComputeLengthSize(arraySize) + arraySize;
            default:
                var defaultRawStringVal = Convert.ToString(value); // , CultureInfo.InvariantCulture);
                var defaultStringVal = defaultRawStringVal;
                if (defaultRawStringVal?.Length > stringSizeLimit)
                {
                    defaultStringVal = defaultRawStringVal.Substring(0, stringSizeLimit);
                }

                return ComputeStringWithTagSize(FieldNumberConstants.AnyValue_string_value, defaultStringVal!);
        }
    }

    internal int ComputeArrayValueSize(Array array)
    {
        int size = 0;
        foreach (var value in array)
        {
            var anyValueSize = this.ComputeAnyValueSize(value);

            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.ArrayValue_Value);
            size += WireTypesSizeCalculator.ComputeLengthSize(anyValueSize); // length prefix for any value pair.
            size += anyValueSize;
        }

        return size;
    }

    internal int ComputeScopeSize(string activitySourceName, string? activitySourceVersion, List<Activity> scopeActivities)
    {
        int size = 0;
        var instrumentationScopeSize = this.ComputeInstrumentationScopeSize(activitySourceName, activitySourceVersion);
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.ScopeSpans_scope);
        size += WireTypesSizeCalculator.ComputeLengthSize(instrumentationScopeSize);
        size += instrumentationScopeSize;

        foreach (var activity in scopeActivities)
        {
            var activitySize = this.ComputeActivitySize(activity);
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.ScopeSpans_span);
            size += WireTypesSizeCalculator.ComputeLengthSize(activitySize);
            size += activitySize;
        }

        return size;
    }

    internal int ComputeInstrumentationScopeSize(string activitySourceName, string? activitySourceVersion)
    {
        int size = 0;

        size += ComputeStringWithTagSize(FieldNumberConstants.InstrumentationScope_name, activitySourceName);

        if (activitySourceVersion != null)
        {
            size += ComputeStringWithTagSize(FieldNumberConstants.InstrumentationScope_version, activitySourceVersion);
        }

        return size;
    }

    internal int ComputeResourceSize(Resource resource)
    {
        int size = 0;
        if (resource != null && resource != Resource.Empty)
        {
            foreach (var attribute in resource.Attributes)
            {
                var keyValueSize = this.ComputeKeyValuePairSize(attribute!);
                size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Resource_attributes);
                size += WireTypesSizeCalculator.ComputeLengthSize(keyValueSize); // length prefix for key value pair.
                size += keyValueSize;
            }
        }

        return size;
    }

    private static int ComputeStringWithTagSize(int fieldNumber, string value)
    {
        int size = 0;
        size += WireTypesSizeCalculator.ComputeTagSize(fieldNumber);
        var stringLength = Encoding.UTF8.GetByteCount(value);
        size += WireTypesSizeCalculator.ComputeLengthSize(stringLength);
        size += stringLength;

        return size;
    }
}
