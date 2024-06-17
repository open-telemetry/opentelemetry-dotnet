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

        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_name);
        var displayLength = Encoding.UTF8.GetByteCount(activity.DisplayName);
        size += WireTypesSizeCalculator.ComputeLengthSize(displayLength);
        size += displayLength;

        if (activity.TraceStateString != null)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_trace_state);
            var traceStateLength = Encoding.UTF8.GetByteCount(activity.TraceStateString);
            size += WireTypesSizeCalculator.ComputeLengthSize(traceStateLength);
            size += traceStateLength;
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
            size += WireTypesSizeCalculator.ComputeRawVarint32Size((uint)droppedEventCount);
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_dropped_events_count);
        }

        size += this.ComputeActivityLinksSize(activity, out var droppedLinkCount);
        if (droppedLinkCount > 0)
        {
            size += WireTypesSizeCalculator.ComputeRawVarint32Size((uint)droppedLinkCount);
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_dropped_links_count);
        }

        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_flags);
        size += 4;

        // TODO: other fields.

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
                var statusDescLength = Encoding.UTF8.GetByteCount(activity.StatusDescription!);
                size += WireTypesSizeCalculator.ComputeLengthSize(statusDescLength) + statusDescLength + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Status_message);
            }
        }
        else if (statusCode != StatusCode.Unset)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Status_code);
            size += 1;

            if (statusCode == StatusCode.Error && statusMessage != null)
            {
                var statusDescLength = Encoding.UTF8.GetByteCount(statusMessage!);
                size += WireTypesSizeCalculator.ComputeLengthSize(statusDescLength) + statusDescLength + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Status_message);
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
                    size += linkSize;
                    size += WireTypesSizeCalculator.ComputeLengthSize(linkSize);
                    size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_links);

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
        size += 16;
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Link_span_id);
        size += WireTypesSizeCalculator.ComputeLengthSize(8);
        size += 8;

        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Link_flags);
        size += 4;

        int droppedAttributeCount = 0;
        int attributeCount = 0;

        foreach (var tag in link.EnumerateTagObjects())
        {
            if (attributeCount < this.sdkLimitOptions.SpanLinkAttributeCountLimit)
            {
                var keyValueSize = this.ComputeKeyValuePairSize(tag);
                size += keyValueSize;
                size += WireTypesSizeCalculator.ComputeLengthSize(keyValueSize); // length prefix for key value pair.
                size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Link_attributes);
                attributeCount++;
            }
            else
            {
                droppedAttributeCount++;
            }
        }

        if (droppedAttributeCount > 0)
        {
            size += WireTypesSizeCalculator.ComputeLengthSize(droppedAttributeCount);
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Link_dropped_attributes_count);
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
                    size += evntSize;
                    size += WireTypesSizeCalculator.ComputeLengthSize(evntSize);
                    size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_events);
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
        int eventNameLength = Encoding.UTF8.GetByteCount(evnt.Name);
        size += WireTypesSizeCalculator.ComputeLengthSize(eventNameLength);
        size += eventNameLength;
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Event_name);
        size += TimeSize; // time
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Event_time_unix_nano);
        foreach (var tag in evnt.EnumerateTagObjects())
        {
            if (attributeCount < spanEventAttributeCountLimit)
            {
                var keyValueSize = this.ComputeKeyValuePairSize(tag);
                size += keyValueSize;
                size += WireTypesSizeCalculator.ComputeLengthSize(keyValueSize); // length prefix for key value pair.
                size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Event_attributes);
                attributeCount++;
            }
            else
            {
                droppedAttributeCount++;
            }
        }

        if (droppedAttributeCount > 0)
        {
            size += WireTypesSizeCalculator.ComputeLengthSize(droppedAttributeCount);
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Event_dropped_attributes_count);
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
                size += keyValueSize;
                size += WireTypesSizeCalculator.ComputeLengthSize(keyValueSize); // length prefix for key value pair.
                size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Span_attributes);
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
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.KeyValue_key); // key tag;
        int keyLength = Encoding.UTF8.GetByteCount(tag.Key);
        size += WireTypesSizeCalculator.ComputeLengthSize(keyLength);
        size += keyLength;
        var anyValueSize = this.ComputeAnyValueSize(tag.Value!);

        // size += 1; // anyvalue tag size;
        size += WireTypesSizeCalculator.ComputeLengthSize(anyValueSize); // length prefix for any value pair.
        size += anyValueSize;
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.KeyValue_value);

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
                var charVal = Convert.ToString(value, CultureInfo.InvariantCulture);
                var charLength = Encoding.UTF8.GetByteCount(charVal!);
                return WireTypesSizeCalculator.ComputeLengthSize(charLength) + charLength + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_string_value);
            case string:
                var rawStringVal = Convert.ToString(value, CultureInfo.InvariantCulture);
                var stringVal = rawStringVal;
                if (rawStringVal?.Length > stringSizeLimit)
                {
                    stringVal = rawStringVal.Substring(0, stringSizeLimit);
                }

                var length = Encoding.UTF8.GetByteCount(stringVal!);
                return WireTypesSizeCalculator.ComputeLengthSize(length) + length + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_string_value);
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

                var defaultLength = Encoding.UTF8.GetByteCount(defaultStringVal!);
                return WireTypesSizeCalculator.ComputeLengthSize(defaultLength) + defaultLength + WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.AnyValue_string_value);
        }
    }

    internal int ComputeArrayValueSize(Array array)
    {
        int size = 0;
        foreach (var value in array)
        {
            var anyValueSize = this.ComputeAnyValueSize(value);

            size += WireTypesSizeCalculator.ComputeLengthSize(anyValueSize); // length prefix for any value pair.
            size += anyValueSize;
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.ArrayValue_Value);
        }

        return size;
    }

    internal int ComputeScopeSize(string activitySourceName, string? activitySourceVersion, List<Activity> scopeActivities)
    {
        int size = 0;
        var instrumentationScopeSize = this.ComputeInstrumentationScopeSize(activitySourceName, activitySourceVersion);
        size += instrumentationScopeSize;
        size += WireTypesSizeCalculator.ComputeLengthSize(instrumentationScopeSize);
        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.ScopeSpans_scope);

        foreach (var activity in scopeActivities)
        {
            var activitySize = this.ComputeActivitySize(activity);
            size += activitySize;
            size += WireTypesSizeCalculator.ComputeLengthSize(activitySize);
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.ScopeSpans_span);
        }

        return size;
    }

    internal int ComputeInstrumentationScopeSize(string activitySourceName, string? activitySourceVersion)
    {
        int size = 0;

        size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.InstrumentationScope_name);
        var scopeNameLength = Encoding.UTF8.GetByteCount(activitySourceName);
        size += WireTypesSizeCalculator.ComputeLengthSize(scopeNameLength);
        size += scopeNameLength;
        if (activitySourceVersion != null)
        {
            size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.InstrumentationScope_version);
            var scopeVersionLength = Encoding.UTF8.GetByteCount(activitySourceVersion);
            size += WireTypesSizeCalculator.ComputeLengthSize(scopeVersionLength);
            size += scopeVersionLength;
        }

        return size;
    }

    internal int ComputeResourceSize(Resource resource)
    {
        int size = 0;
        if (resource != Resource.Empty)
        {
            foreach (var attribute in resource.Attributes)
            {
                var keyValueSize = this.ComputeKeyValuePairSize(attribute!);
                size += keyValueSize;
                size += WireTypesSizeCalculator.ComputeLengthSize(keyValueSize); // length prefix for key value pair.
                size += WireTypesSizeCalculator.ComputeTagSize(FieldNumberConstants.Resource_attributes);
            }
        }

        return size;
    }
}
