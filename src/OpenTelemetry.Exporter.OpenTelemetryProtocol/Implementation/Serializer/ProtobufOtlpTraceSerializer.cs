// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpTraceSerializer
{
    private const int ReserveSizeForLength = 4;
    private const string UnsetStatusCodeTagValue = "UNSET";
    private const string OkStatusCodeTagValue = "OK";
    private const string ErrorStatusCodeTagValue = "ERROR";
    private const int TraceIdSize = 16;
    private const int SpanIdSize = 8;

    [ThreadStatic]
    private static Stack<List<Activity>>? activityListPool;
    [ThreadStatic]
    private static Dictionary<string, List<Activity>>? scopeTracesList;

    internal static int WriteTraceData(ref byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Resources.Resource? resource, in Batch<Activity> batch)
    {
        activityListPool ??= [];
        scopeTracesList ??= [];

        foreach (var activity in batch)
        {
            var sourceName = activity.Source.Name;
            if (!scopeTracesList.TryGetValue(sourceName, out var activities))
            {
                activities = activityListPool.Count > 0 ? activityListPool.Pop() : [];
                scopeTracesList[sourceName] = activities;
            }

            activities.Add(activity);
        }

        writePosition = TryWriteResourceSpans(ref buffer, writePosition, sdkLimitOptions, resource);
        ReturnActivityListToPool();

        return writePosition;
    }

    internal static int TryWriteResourceSpans(ref byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Resources.Resource? resource)
    {
        while (true)
        {
            int entryWritePosition = writePosition;

            try
            {
                writePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.TracesData_Resource_Spans, ProtobufWireType.LEN);
                int resourceSpansScopeSpansLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;

                writePosition = WriteResourceSpans(buffer, writePosition, sdkLimitOptions, resource);

                ProtobufSerializer.WriteReservedLength(buffer.AsSpan(resourceSpansScopeSpansLengthPosition), writePosition - (resourceSpansScopeSpansLengthPosition + ReserveSizeForLength));

                // Serialization succeeded, return the final write position
                return writePosition;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentException)
            {
                // Reset write position and attempt to increase the buffer size
                writePosition = entryWritePosition;

                if (!ProtobufSerializer.IncreaseBufferSize(ref buffer, OtlpSignalType.Traces))
                {
                    throw;
                }

                // Continue the loop to retry serialization with the larger buffer
                // The loop is limited by the buffer size expansion logic in IncreaseBufferSize,
                // which stops at a maximum of 100 MB, ensuring this doesn't become an infinite loop
            }
        }
    }

    internal static void ReturnActivityListToPool()
    {
        if (scopeTracesList?.Count != 0)
        {
            foreach (var entry in scopeTracesList!)
            {
                entry.Value.Clear();
                activityListPool?.Push(entry.Value);
            }

            scopeTracesList.Clear();
        }
    }

    internal static int WriteResourceSpans(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Resources.Resource? resource)
    {
        writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, writePosition, resource);
        writePosition = WriteScopeSpans(buffer, writePosition, sdkLimitOptions);

        return writePosition;
    }

    internal static int WriteScopeSpans(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions)
    {
        if (scopeTracesList != null)
        {
            foreach (KeyValuePair<string, List<Activity>> entry in scopeTracesList)
            {
                writePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.ResourceSpans_Scope_Spans, ProtobufWireType.LEN);
                int resourceSpansScopeSpansLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;

                writePosition = WriteScopeSpan(buffer, writePosition, sdkLimitOptions, entry.Value[0].Source, entry.Value);
                ProtobufSerializer.WriteReservedLength(buffer.AsSpan(resourceSpansScopeSpansLengthPosition), writePosition - (resourceSpansScopeSpansLengthPosition + ReserveSizeForLength));
            }
        }

        return writePosition;
    }

    internal static int WriteScopeSpan(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ActivitySource activitySource, List<Activity> activities)
    {
        writePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.ScopeSpans_Scope, ProtobufWireType.LEN);
        int instrumentationScopeLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition += ProtobufSerializer.WriteStringWithTag(buffer.AsSpan(writePosition), ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Name, activitySource.Name);
        if (activitySource.Version != null)
        {
            writePosition += ProtobufSerializer.WriteStringWithTag(buffer.AsSpan(writePosition), ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Version, activitySource.Version);
        }

        if (activitySource.Tags != null)
        {
            var maxAttributeCount = sdkLimitOptions.SpanAttributeCountLimit ?? int.MaxValue;
            var maxAttributeValueLength = sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
            ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
            {
                Buffer = buffer,
                WritePosition = writePosition,
                TagCount = 0,
                DroppedTagCount = 0,
            };

            if (activitySource.Tags is IReadOnlyList<KeyValuePair<string, object?>> activitySourceTagsList)
            {
                for (int i = 0; i < activitySourceTagsList.Count; i++)
                {
                    if (otlpTagWriterState.TagCount < maxAttributeCount)
                    {
                        otlpTagWriterState.WritePosition += ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Attributes, ProtobufWireType.LEN);
                        int instrumentationScopeAttributesLengthPosition = otlpTagWriterState.WritePosition;
                        otlpTagWriterState.WritePosition += ReserveSizeForLength;

                        ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, activitySourceTagsList[i].Key, activitySourceTagsList[i].Value, maxAttributeValueLength);

                        var instrumentationScopeAttributesLength = otlpTagWriterState.WritePosition - (instrumentationScopeAttributesLengthPosition + ReserveSizeForLength);
                        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer.AsSpan(instrumentationScopeAttributesLengthPosition), instrumentationScopeAttributesLength);
                        otlpTagWriterState.TagCount++;
                    }
                    else
                    {
                        otlpTagWriterState.DroppedTagCount++;
                    }
                }
            }
            else
            {
                foreach (var tag in activitySource.Tags)
                {
                    if (otlpTagWriterState.TagCount < maxAttributeCount)
                    {
                        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Attributes, ProtobufWireType.LEN);
                        int instrumentationScopeAttributesLengthPosition = otlpTagWriterState.WritePosition;
                        otlpTagWriterState.WritePosition += ReserveSizeForLength;

                        ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, tag.Key, tag.Value, maxAttributeValueLength);

                        var instrumentationScopeAttributesLength = otlpTagWriterState.WritePosition - (instrumentationScopeAttributesLengthPosition + ReserveSizeForLength);
                        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer.AsSpan(instrumentationScopeAttributesLengthPosition), instrumentationScopeAttributesLength);
                        otlpTagWriterState.TagCount++;
                    }
                    else
                    {
                        otlpTagWriterState.DroppedTagCount++;
                    }
                }
            }

            if (otlpTagWriterState.DroppedTagCount > 0)
            {
                otlpTagWriterState.WritePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Dropped_Attributes_Count, ProtobufWireType.VARINT);
                otlpTagWriterState.WritePosition += ProtobufSerializer.WriteVarInt32(buffer.AsSpan(otlpTagWriterState.WritePosition), (uint)otlpTagWriterState.DroppedTagCount);
            }

            writePosition = otlpTagWriterState.WritePosition;
        }

        ProtobufSerializer.WriteReservedLength(buffer.AsSpan(instrumentationScopeLengthPosition), writePosition - (instrumentationScopeLengthPosition + ReserveSizeForLength));

        for (int i = 0; i < activities.Count; i++)
        {
            writePosition = WriteSpan(buffer, writePosition, sdkLimitOptions, activities[i]);
        }

        return writePosition;
    }

    internal static int WriteSpan(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Activity activity)
    {
        writePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.ScopeSpans_Span, ProtobufWireType.LEN);
        int spanLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition += ProtobufSerializer.WriteTagAndLength(buffer.AsSpan(writePosition), TraceIdSize, ProtobufOtlpTraceFieldNumberConstants.Span_Trace_Id, ProtobufWireType.LEN);
        writePosition = WriteTraceId(buffer, writePosition, activity.TraceId);

        writePosition += ProtobufSerializer.WriteTagAndLength(buffer.AsSpan(writePosition), SpanIdSize, ProtobufOtlpTraceFieldNumberConstants.Span_Span_Id, ProtobufWireType.LEN);
        writePosition = WriteSpanId(buffer, writePosition, activity.SpanId);

        if (activity.TraceStateString != null)
        {
            writePosition += ProtobufSerializer.WriteStringWithTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Trace_State, activity.TraceStateString);
        }

        if (activity.ParentSpanId != default)
        {
            writePosition += ProtobufSerializer.WriteTagAndLength(buffer.AsSpan(writePosition), SpanIdSize, ProtobufOtlpTraceFieldNumberConstants.Span_Parent_Span_Id, ProtobufWireType.LEN);
            writePosition = WriteSpanId(buffer, writePosition, activity.ParentSpanId);
        }

        writePosition = WriteTraceFlags(buffer, writePosition, activity.ActivityTraceFlags, activity.HasRemoteParent, ProtobufOtlpTraceFieldNumberConstants.Span_Flags);
        writePosition += ProtobufSerializer.WriteStringWithTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Name, activity.DisplayName);
        writePosition += ProtobufSerializer.WriteEnumWithTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Kind, (int)activity.Kind + 1);
        writePosition += ProtobufSerializer.WriteFixed64WithTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Start_Time_Unix_Nano, (ulong)activity.StartTimeUtc.ToUnixTimeNanoseconds());
        writePosition += ProtobufSerializer.WriteFixed64WithTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Span_End_Time_Unix_Nano, (ulong)(activity.StartTimeUtc.ToUnixTimeNanoseconds() + activity.Duration.ToNanoseconds()));

        (writePosition, StatusCode? statusCode, string? statusMessage) = WriteActivityTags(buffer, writePosition, sdkLimitOptions, activity);
        writePosition = WriteSpanEvents(buffer, writePosition, sdkLimitOptions, activity);
        writePosition = WriteSpanLinks(buffer, writePosition, sdkLimitOptions, activity);
        writePosition = WriteSpanStatus(buffer, writePosition, activity, statusCode, statusMessage);
        ProtobufSerializer.WriteReservedLength(buffer.AsSpan(spanLengthPosition), writePosition - (spanLengthPosition + ReserveSizeForLength));

        return writePosition;
    }

    internal static int WriteTraceId(byte[] buffer, int position, ActivityTraceId activityTraceId)
    {
        var traceBytes = new Span<byte>(buffer, position, TraceIdSize);
        activityTraceId.CopyTo(traceBytes);
        return position + TraceIdSize;
    }

    internal static int WriteSpanId(byte[] buffer, int position, ActivitySpanId activitySpanId)
    {
        var spanIdBytes = new Span<byte>(buffer, position, SpanIdSize);
        activitySpanId.CopyTo(spanIdBytes);
        return position + SpanIdSize;
    }

    internal static int WriteTraceFlags(byte[] buffer, int position, ActivityTraceFlags activityTraceFlags, bool hasRemoteParent, int fieldNumber)
    {
        uint spanFlags = (uint)activityTraceFlags & (byte)0x000000FF;

        spanFlags |= 0x00000100;
        if (hasRemoteParent)
        {
            spanFlags |= 0x00000200;
        }

        position += ProtobufSerializer.WriteFixed32WithTag(buffer.AsSpan(position), fieldNumber, spanFlags);

        return position;
    }

    internal static (int Position, StatusCode? StatusCode, string? StatusMessage) WriteActivityTags(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Activity activity)
    {
        StatusCode? statusCode = null;
        string? statusMessage = null;
        int maxAttributeCount = sdkLimitOptions.SpanAttributeCountLimit ?? int.MaxValue;
        int maxAttributeValueLength = sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
        ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
            TagCount = 0,
            DroppedTagCount = 0,
        };

        foreach (ref readonly var tag in activity.EnumerateTagObjects())
        {
            switch (tag.Key)
            {
                case "otel.status_code":

                    statusCode = tag.Value switch
                    {
                        /*
                         * Note: Order here does matter for perf. Unset is
                         * first because assumption is most spans will be
                         * Unset, then Error. Ok is not set by the SDK.
                         */
                        not null when UnsetStatusCodeTagValue.Equals(tag.Value as string, StringComparison.OrdinalIgnoreCase) => StatusCode.Unset,
                        not null when ErrorStatusCodeTagValue.Equals(tag.Value as string, StringComparison.OrdinalIgnoreCase) => StatusCode.Error,
                        not null when OkStatusCodeTagValue.Equals(tag.Value as string, StringComparison.OrdinalIgnoreCase) => StatusCode.Ok,
                        _ => null,
                    };
                    continue;
                case "otel.status_description":
                    statusMessage = tag.Value as string;
                    continue;
            }

            if (otlpTagWriterState.TagCount < maxAttributeCount)
            {
                otlpTagWriterState.WritePosition += ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Attributes, ProtobufWireType.LEN);
                int spanAttributesLengthPosition = otlpTagWriterState.WritePosition;
                otlpTagWriterState.WritePosition += ReserveSizeForLength;

                ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, tag.Key, tag.Value, maxAttributeValueLength);

                ProtobufSerializer.WriteReservedLength(buffer.AsSpan(spanAttributesLengthPosition), otlpTagWriterState.WritePosition - (spanAttributesLengthPosition + 4));
                otlpTagWriterState.TagCount++;
            }
            else
            {
                otlpTagWriterState.DroppedTagCount++;
            }
        }

        if (otlpTagWriterState.DroppedTagCount > 0)
        {
            otlpTagWriterState.WritePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Dropped_Attributes_Count, ProtobufWireType.VARINT);
            otlpTagWriterState.WritePosition += ProtobufSerializer.WriteVarInt32(buffer.AsSpan(otlpTagWriterState.WritePosition), (uint)otlpTagWriterState.DroppedTagCount);
        }

        return (otlpTagWriterState.WritePosition, statusCode, statusMessage);
    }

    internal static int WriteSpanEvents(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Activity activity)
    {
        int maxEventCountLimit = sdkLimitOptions.SpanEventCountLimit ?? int.MaxValue;
        int eventCount = 0;
        int droppedEventCount = 0;
        foreach (ref readonly var evnt in activity.EnumerateEvents())
        {
            if (eventCount < maxEventCountLimit)
            {
                writePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Events, ProtobufWireType.LEN);
                int spanEventsLengthPosition = writePosition;
                writePosition += ReserveSizeForLength; // Reserve 4 bytes for length

                writePosition += ProtobufSerializer.WriteStringWithTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Event_Name, evnt.Name);
                writePosition += ProtobufSerializer.WriteFixed64WithTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Event_Time_Unix_Nano, (ulong)evnt.Timestamp.ToUnixTimeNanoseconds());
                writePosition = WriteEventAttributes(ref buffer, writePosition, sdkLimitOptions, evnt);

                ProtobufSerializer.WriteReservedLength(buffer.AsSpan(spanEventsLengthPosition), writePosition - (spanEventsLengthPosition + ReserveSizeForLength));
                eventCount++;
            }
            else
            {
                droppedEventCount++;
            }
        }

        if (droppedEventCount > 0)
        {
            writePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Dropped_Events_Count, ProtobufWireType.VARINT);
            writePosition += ProtobufSerializer.WriteVarInt32(buffer.AsSpan(writePosition), (uint)droppedEventCount);
        }

        return writePosition;
    }

    internal static int WriteEventAttributes(ref byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ActivityEvent evnt)
    {
        int maxAttributeCount = sdkLimitOptions.SpanEventAttributeCountLimit ?? int.MaxValue;
        int maxAttributeValueLength = sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;

        ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
            TagCount = 0,
            DroppedTagCount = 0,
        };

        foreach (ref readonly var tag in evnt.EnumerateTagObjects())
        {
            if (otlpTagWriterState.TagCount < maxAttributeCount)
            {
                otlpTagWriterState.WritePosition += ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpTraceFieldNumberConstants.Event_Attributes, ProtobufWireType.LEN);
                int eventAttributesLengthPosition = otlpTagWriterState.WritePosition;
                otlpTagWriterState.WritePosition += ReserveSizeForLength;
                ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, tag.Key, tag.Value, maxAttributeValueLength);
                ProtobufSerializer.WriteReservedLength(buffer.AsSpan(eventAttributesLengthPosition), otlpTagWriterState.WritePosition - (eventAttributesLengthPosition + ReserveSizeForLength));
                otlpTagWriterState.TagCount++;
            }
            else
            {
                otlpTagWriterState.DroppedTagCount++;
            }
        }

        if (otlpTagWriterState.DroppedTagCount > 0)
        {
            otlpTagWriterState.WritePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpTraceFieldNumberConstants.Event_Dropped_Attributes_Count, ProtobufWireType.VARINT);
            otlpTagWriterState.WritePosition += ProtobufSerializer.WriteVarInt32(buffer.AsSpan(otlpTagWriterState.WritePosition), (uint)otlpTagWriterState.DroppedTagCount);
        }

        return otlpTagWriterState.WritePosition;
    }

    internal static int WriteSpanLinks(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Activity activity)
    {
        int maxLinksCount = sdkLimitOptions.SpanLinkCountLimit ?? int.MaxValue;
        int linkCount = 0;
        int droppedLinkCount = 0;

        foreach (ref readonly var link in activity.EnumerateLinks())
        {
            if (linkCount < maxLinksCount)
            {
                writePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Links, ProtobufWireType.LEN);
                int spanLinksLengthPosition = writePosition;
                writePosition += ReserveSizeForLength; // Reserve 4 bytes for length

                writePosition += ProtobufSerializer.WriteTagAndLength(buffer.AsSpan(writePosition), TraceIdSize, ProtobufOtlpTraceFieldNumberConstants.Link_Trace_Id, ProtobufWireType.LEN);
                writePosition = WriteTraceId(buffer, writePosition, link.Context.TraceId);
                writePosition += ProtobufSerializer.WriteTagAndLength(buffer.AsSpan(writePosition), SpanIdSize, ProtobufOtlpTraceFieldNumberConstants.Link_Span_Id, ProtobufWireType.LEN);
                writePosition = WriteSpanId(buffer, writePosition, link.Context.SpanId);
                if (link.Context.TraceState != null)
                {
                    writePosition += ProtobufSerializer.WriteStringWithTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Trace_State, link.Context.TraceState);
                }

                writePosition = WriteLinkAttributes(buffer, writePosition, sdkLimitOptions, link);
                writePosition = WriteTraceFlags(buffer, writePosition, link.Context.TraceFlags, link.Context.IsRemote, ProtobufOtlpTraceFieldNumberConstants.Link_Flags);

                ProtobufSerializer.WriteReservedLength(buffer.AsSpan(spanLinksLengthPosition), writePosition - (spanLinksLengthPosition + ReserveSizeForLength));
                linkCount++;
            }
            else
            {
                droppedLinkCount++;
            }
        }

        if (droppedLinkCount > 0)
        {
            writePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(writePosition), ProtobufOtlpTraceFieldNumberConstants.Span_Dropped_Links_Count, ProtobufWireType.VARINT);
            writePosition += ProtobufSerializer.WriteVarInt32(buffer.AsSpan(writePosition), (uint)droppedLinkCount);
        }

        return writePosition;
    }

    internal static int WriteLinkAttributes(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ActivityLink link)
    {
        int maxAttributeCount = sdkLimitOptions.SpanLinkAttributeCountLimit ?? int.MaxValue;
        int maxAttributeValueLength = sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
        ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
            TagCount = 0,
            DroppedTagCount = 0,
        };

        foreach (ref readonly var tag in link.EnumerateTagObjects())
        {
            if (otlpTagWriterState.TagCount < maxAttributeCount)
            {
                otlpTagWriterState.WritePosition += ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpTraceFieldNumberConstants.Link_Attributes, ProtobufWireType.LEN);
                int linkAttributesLengthPosition = otlpTagWriterState.WritePosition;
                otlpTagWriterState.WritePosition += ReserveSizeForLength;
                ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, tag.Key, tag.Value, maxAttributeValueLength);
                ProtobufSerializer.WriteReservedLength(buffer.AsSpan(linkAttributesLengthPosition), otlpTagWriterState.WritePosition - (linkAttributesLengthPosition + ReserveSizeForLength));
                otlpTagWriterState.TagCount++;
            }
            else
            {
                otlpTagWriterState.DroppedTagCount++;
            }
        }

        if (otlpTagWriterState.DroppedTagCount > 0)
        {
            otlpTagWriterState.WritePosition += ProtobufSerializer.WriteTag(buffer.AsSpan(otlpTagWriterState.WritePosition), ProtobufOtlpTraceFieldNumberConstants.Link_Dropped_Attributes_Count, ProtobufWireType.VARINT);
            otlpTagWriterState.WritePosition += ProtobufSerializer.WriteVarInt32(buffer.AsSpan(otlpTagWriterState.WritePosition), (uint)otlpTagWriterState.DroppedTagCount);
        }

        return otlpTagWriterState.WritePosition;
    }

    internal static int WriteSpanStatus(byte[] buffer, int position, Activity activity, StatusCode? statusCode, string? statusMessage)
    {
        if (activity.Status == ActivityStatusCode.Unset && statusCode == null)
        {
            return position;
        }

        var useActivity = activity.Status != ActivityStatusCode.Unset;
        var isError = useActivity ? activity.Status == ActivityStatusCode.Error : statusCode == StatusCode.Error;
        var description = useActivity ? activity.StatusDescription : statusMessage;

        if (isError && description != null)
        {
            var descriptionSpan = description.AsSpan();
            var numberOfUtf8CharsInString = ProtobufSerializer.GetNumberOfUtf8CharsInString(descriptionSpan);
            var serializedLengthSize = ProtobufSerializer.ComputeVarInt64Size((ulong)numberOfUtf8CharsInString);

            // length = numberOfUtf8CharsInString + Status_Message tag size + serializedLengthSize field size + Span_Status tag size + Span_Status length size.
            position += ProtobufSerializer.WriteTagAndLength(buffer.AsSpan(position), numberOfUtf8CharsInString + 1 + serializedLengthSize + 2, ProtobufOtlpTraceFieldNumberConstants.Span_Status, ProtobufWireType.LEN);
            position += ProtobufSerializer.WriteStringWithTag(buffer.AsSpan(position), ProtobufOtlpTraceFieldNumberConstants.Status_Message, numberOfUtf8CharsInString, descriptionSpan);
        }
        else
        {
            position += ProtobufSerializer.WriteTagAndLength(buffer.AsSpan(position), 2, ProtobufOtlpTraceFieldNumberConstants.Span_Status, ProtobufWireType.LEN);
        }

        var finalStatusCode = useActivity ? (int)activity.Status : (statusCode != null && statusCode != StatusCode.Unset) ? (int)statusCode! : (int)StatusCode.Unset;
        position += ProtobufSerializer.WriteEnumWithTag(buffer.AsSpan(position), ProtobufOtlpTraceFieldNumberConstants.Status_Code, finalStatusCode);

        return position;
    }
}
