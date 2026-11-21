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
    private static Dictionary<InstrumentationScope, List<Activity>>? scopeTracesList;

    // Note: ActivitySource does not override Equals or GetHashCode, so this
    // dictionary compares sources by reference. It saves building and hashing
    // an InstrumentationScope for every source change in an interleaved batch.
    [ThreadStatic]
    private static Dictionary<ActivitySource, List<Activity>>? sourceTracesList;

    internal static int WriteTraceData(
        ref byte[] buffer,
        int writePosition,
        SdkLimitOptions sdkLimitOptions,
        Resources.Resource? resource,
        in Batch<Activity> batch,
        out int dirtyLength,
        int maxBufferSize = ProtobufSerializer.MaxBufferSize)
    {
        dirtyLength = 0;
        activityListPool ??= [];
        scopeTracesList ??= [];
        sourceTracesList ??= [];

        // Note: The grouped batch is held in thread-static state, so it has to be
        // released even when serialization fails. TryWriteResourceSpans rethrows
        // once the buffer cannot be grown any further; leaving the batch behind
        // would merge it into the next export on this thread.
        try
        {
            // Note: Consecutive activities very often come from the same source,
            // so the previous lookup is reused until the source changes. Sources
            // are only cached by reference once a second source is seen, so a
            // batch from a single source does not pay for that cache.
            ActivitySource? lastSource = null;
            List<Activity>? activities = null;

            foreach (var activity in batch)
            {
                var source = activity.Source;
                if (activities == null || !ReferenceEquals(source, lastSource))
                {
                    if (lastSource != null)
                    {
                        sourceTracesList[lastSource] = activities!;
                    }

                    if (!sourceTracesList.TryGetValue(source, out activities))
                    {
                        var scope = new InstrumentationScope(source);
                        if (!scopeTracesList.TryGetValue(scope, out activities))
                        {
                            activities = activityListPool.Count > 0 ? activityListPool.Pop() : [];
                            scopeTracesList[scope] = activities;
                        }
                    }

                    lastSource = source;
                }

                activities.Add(activity);
            }

            writePosition = TryWriteResourceSpans(ref buffer, writePosition, sdkLimitOptions, resource, ref dirtyLength, maxBufferSize);
        }
        finally
        {
            ReturnActivityListToPool();
        }

        return writePosition;
    }

    internal static int TryWriteResourceSpans(
        ref byte[] buffer,
        int writePosition,
        SdkLimitOptions sdkLimitOptions,
        Resources.Resource? resource,
        ref int dirtyLength,
        int maxBufferSize = ProtobufSerializer.MaxBufferSize)
    {
        while (true)
        {
            var entryWritePosition = writePosition;

            try
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.TracesData_Resource_Spans, ProtobufWireType.LEN);
                var resourceSpansScopeSpansLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;

                writePosition = WriteResourceSpans(buffer, writePosition, sdkLimitOptions, resource, ref dirtyLength);

                ProtobufSerializer.WriteReservedLength(buffer, resourceSpansScopeSpansLengthPosition, writePosition - (resourceSpansScopeSpansLengthPosition + ReserveSizeForLength));

                // Serialization succeeded, return the final write position
                return writePosition;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                // Reset write position and attempt to increase the buffer size
                writePosition = entryWritePosition;
                dirtyLength = 0;

                if (!ProtobufSerializer.IncreaseBufferSize(ref buffer, OtlpSignalType.Traces, maxBufferSize))
                {
                    throw;
                }

                // Continue the loop to retry serialization with the larger buffer. The loop
                // is bounded by IncreaseBufferSize, which refuses to grow beyond beyond
                // ProtobufSerializer.MaxBufferSize, so this cannot become an infinite loop.
            }
        }
    }

    internal static void ReturnActivityListToPool()
    {
        if (scopeTracesList is { Count: > 0 })
        {
            foreach (var entry in scopeTracesList)
            {
                entry.Value.Clear();
                activityListPool?.Push(entry.Value);
            }

            scopeTracesList.Clear();
        }

        sourceTracesList?.Clear();
    }

    internal static int WriteResourceSpans(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Resources.Resource? resource, ref int dirtyLength)
    {
        writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, writePosition, resource);
        writePosition = WriteScopeSpans(buffer, writePosition, sdkLimitOptions, ref dirtyLength);

        if (resource?.SchemaUrl is { Length: > 0 } schemaUrl)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ResourceSpans_Schema_Url, schemaUrl);
        }

        return writePosition;
    }

    internal static int WriteScopeSpans(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ref int dirtyLength)
    {
        if (scopeTracesList != null)
        {
            foreach (var entry in scopeTracesList)
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ResourceSpans_Scope_Spans, ProtobufWireType.LEN);
                var resourceSpansScopeSpansLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;

                writePosition = WriteScopeSpan(buffer, writePosition, sdkLimitOptions, entry.Value[0].Source, entry.Value, ref dirtyLength);
                ProtobufSerializer.WriteReservedLength(buffer, resourceSpansScopeSpansLengthPosition, writePosition - (resourceSpansScopeSpansLengthPosition + ReserveSizeForLength));
            }
        }

        return writePosition;
    }

    internal static int WriteScopeSpan(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ActivitySource activitySource, List<Activity> activities, ref int dirtyLength)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ScopeSpans_Scope, ProtobufWireType.LEN);
        var instrumentationScopeLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Name, activitySource.Name);
        if (activitySource.Version != null)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Version, activitySource.Version);
        }

        if (activitySource.Tags != null)
        {
            // Scope attributes use the general limits, not the span limits.
            var maxAttributeCount = sdkLimitOptions.AttributeCountLimit ?? int.MaxValue;
            var maxAttributeValueLength = sdkLimitOptions.AttributeValueLengthLimit ?? int.MaxValue;
            var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
            {
                Buffer = buffer,
                WritePosition = writePosition,
                DirtyLength = dirtyLength,
                TagCount = 0,
                DroppedTagCount = 0,
            };

            if (activitySource.Tags is IReadOnlyList<KeyValuePair<string, object?>> activitySourceTagsList)
            {
                for (var i = 0; i < activitySourceTagsList.Count; i++)
                {
                    if (otlpTagWriterState.TagCount < maxAttributeCount)
                    {
                        if (ProtobufOtlpTagWriter.WriteKeyValue(
                            ref otlpTagWriterState,
                            ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Attributes,
                            activitySourceTagsList[i].Key,
                            activitySourceTagsList[i].Value,
                            maxAttributeValueLength))
                        {
                            otlpTagWriterState.TagCount++;
                        }
                        else
                        {
                            otlpTagWriterState.DroppedTagCount++;
                        }
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
                        if (ProtobufOtlpTagWriter.WriteKeyValue(
                            ref otlpTagWriterState,
                            ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Attributes,
                            tag.Key,
                            tag.Value,
                            maxAttributeValueLength))
                        {
                            otlpTagWriterState.TagCount++;
                        }
                        else
                        {
                            otlpTagWriterState.DroppedTagCount++;
                        }
                    }
                    else
                    {
                        otlpTagWriterState.DroppedTagCount++;
                    }
                }
            }

            if (otlpTagWriterState.DroppedTagCount > 0)
            {
                otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Dropped_Attributes_Count, ProtobufWireType.VARINT);
                otlpTagWriterState.WritePosition = ProtobufSerializer.WriteVarInt32(buffer, otlpTagWriterState.WritePosition, (uint)otlpTagWriterState.DroppedTagCount);
            }

            writePosition = otlpTagWriterState.WritePosition;
            dirtyLength = otlpTagWriterState.DirtyLength;
        }

        ProtobufSerializer.WriteReservedLength(buffer, instrumentationScopeLengthPosition, writePosition - (instrumentationScopeLengthPosition + ReserveSizeForLength));

        for (var i = 0; i < activities.Count; i++)
        {
            writePosition = WriteSpan(buffer, writePosition, sdkLimitOptions, activities[i], ref dirtyLength);
        }

        if (!string.IsNullOrEmpty(activitySource.TelemetrySchemaUrl))
        {
#pragma warning disable IDE0370 // Suppression is unnecessary
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ScopeSpans_Schema_Url, activitySource.TelemetrySchemaUrl!);
#pragma warning restore IDE0370 // Suppression is unnecessary
        }

        return writePosition;
    }

    internal static int WriteSpan(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Activity activity, ref int dirtyLength)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ScopeSpans_Span, ProtobufWireType.LEN);
        var spanLengthPosition = writePosition;
        writePosition += ProtobufSerializer.ReserveSizeForShortLength;

        writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, TraceIdSize, ProtobufOtlpTraceFieldNumberConstants.Span_Trace_Id, ProtobufWireType.LEN);
        writePosition = WriteTraceId(buffer, writePosition, activity.TraceId);

        writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, SpanIdSize, ProtobufOtlpTraceFieldNumberConstants.Span_Span_Id, ProtobufWireType.LEN);
        writePosition = WriteSpanId(buffer, writePosition, activity.SpanId);

        if (activity.TraceStateString != null)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Trace_State, activity.TraceStateString);
        }

        if (activity.ParentSpanId != default)
        {
            writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, SpanIdSize, ProtobufOtlpTraceFieldNumberConstants.Span_Parent_Span_Id, ProtobufWireType.LEN);
            writePosition = WriteSpanId(buffer, writePosition, activity.ParentSpanId);
        }

        writePosition = WriteTraceFlags(buffer, writePosition, activity.ActivityTraceFlags, activity.HasRemoteParent, ProtobufOtlpTraceFieldNumberConstants.Span_Flags);
        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Name, activity.DisplayName);
        writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Kind, (int)activity.Kind + 1);
        var startTimeUnixNano = activity.StartTimeUtc.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Start_Time_Unix_Nano, (ulong)startTimeUnixNano);
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_End_Time_Unix_Nano, (ulong)(startTimeUnixNano + activity.Duration.ToNanoseconds()));

        (writePosition, var statusCode, var statusMessage) = WriteActivityTags(buffer, writePosition, sdkLimitOptions, activity, ref dirtyLength);
        writePosition = WriteSpanEvents(buffer, writePosition, sdkLimitOptions, activity, ref dirtyLength);
        writePosition = WriteSpanLinks(buffer, writePosition, sdkLimitOptions, activity, ref dirtyLength);
        writePosition = WriteSpanStatus(buffer, writePosition, sdkLimitOptions, activity, statusCode, statusMessage);
        writePosition = ProtobufSerializer.WriteShortReservedLength(buffer, spanLengthPosition, writePosition);

        return writePosition;
    }

    internal static int WriteTraceId(byte[] buffer, int position, ActivityTraceId activityTraceId)
    {
        var traceBytes = new Span<byte>(buffer, position, TraceIdSize);
#if NET
        DecodeHex(activityTraceId.ToHexString(), traceBytes);
#else
        activityTraceId.CopyTo(traceBytes);
#endif
        return position + TraceIdSize;
    }

    internal static int WriteSpanId(byte[] buffer, int position, ActivitySpanId activitySpanId)
    {
        var spanIdBytes = new Span<byte>(buffer, position, SpanIdSize);
#if NET
        DecodeHex(activitySpanId.ToHexString(), spanIdBytes);
#else
        activitySpanId.CopyTo(spanIdBytes);
#endif
        return position + SpanIdSize;
    }

    internal static int WriteTraceFlags(byte[] buffer, int position, ActivityTraceFlags activityTraceFlags, bool hasRemoteParent, int fieldNumber)
    {
        var spanFlags = (uint)activityTraceFlags & 0x000000FF;

        spanFlags |= 0x00000100;
        if (hasRemoteParent)
        {
            spanFlags |= 0x00000200;
        }

        position = ProtobufSerializer.WriteFixed32WithTag(buffer, position, fieldNumber, spanFlags);

        return position;
    }

    internal static (int Position, StatusCode? StatusCode, string? StatusMessage) WriteActivityTags(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Activity activity, ref int dirtyLength)
    {
        StatusCode? statusCode = null;
        string? statusMessage = null;
        var maxAttributeCount = sdkLimitOptions.SpanAttributeCountLimit ?? int.MaxValue;
        var maxAttributeValueLength = sdkLimitOptions.SpanAttributeValueLengthLimit ?? int.MaxValue;
        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
            DirtyLength = dirtyLength,
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
                         * Note: Order here matters for performance. Unset
                         * is first because the assumption is most spans will
                         * be Unset, then Error. Ok is not set by the SDK.
                         */
                        not null when string.Equals(UnsetStatusCodeTagValue, tag.Value as string, StringComparison.OrdinalIgnoreCase) => StatusCode.Unset,
                        not null when string.Equals(ErrorStatusCodeTagValue, tag.Value as string, StringComparison.OrdinalIgnoreCase) => StatusCode.Error,
                        not null when string.Equals(OkStatusCodeTagValue, tag.Value as string, StringComparison.OrdinalIgnoreCase) => StatusCode.Ok,
                        _ => null,
                    };
                    continue;
                case "otel.status_description":
                    statusMessage = tag.Value as string;
                    continue;
                default:
                    break;
            }

            if (otlpTagWriterState.TagCount < maxAttributeCount)
            {
                if (ProtobufOtlpTagWriter.WriteKeyValue(
                    ref otlpTagWriterState,
                    ProtobufOtlpTraceFieldNumberConstants.Span_Attributes,
                    tag.Key,
                    tag.Value,
                    maxAttributeValueLength))
                {
                    otlpTagWriterState.TagCount++;
                }
                else
                {
                    otlpTagWriterState.DroppedTagCount++;
                }
            }
            else
            {
                otlpTagWriterState.DroppedTagCount++;
            }
        }

        if (otlpTagWriterState.DroppedTagCount > 0)
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Dropped_Attributes_Count, ProtobufWireType.VARINT);
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteVarInt32(buffer, otlpTagWriterState.WritePosition, (uint)otlpTagWriterState.DroppedTagCount);
        }

        dirtyLength = otlpTagWriterState.DirtyLength;
        return (otlpTagWriterState.WritePosition, statusCode, statusMessage);
    }

    internal static int WriteSpanEvents(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Activity activity, ref int dirtyLength)
    {
        var maxEventCountLimit = sdkLimitOptions.SpanEventCountLimit ?? int.MaxValue;
        var eventCount = 0;
        var droppedEventCount = 0;
        foreach (ref readonly var evnt in activity.EnumerateEvents())
        {
            if (eventCount < maxEventCountLimit)
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Events, ProtobufWireType.LEN);
                var spanEventsLengthPosition = writePosition;
                writePosition += ProtobufSerializer.ReserveSizeForShortLength;

                writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Event_Name, evnt.Name);
                writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Event_Time_Unix_Nano, (ulong)evnt.Timestamp.ToUnixTimeNanoseconds());
                writePosition = WriteEventAttributes(ref buffer, writePosition, sdkLimitOptions, evnt, ref dirtyLength);

                writePosition = ProtobufSerializer.WriteShortReservedLength(buffer, spanEventsLengthPosition, writePosition);
                eventCount++;
            }
            else
            {
                droppedEventCount++;
            }
        }

        if (droppedEventCount > 0)
        {
            writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Dropped_Events_Count, ProtobufWireType.VARINT);
            writePosition = ProtobufSerializer.WriteVarInt32(buffer, writePosition, (uint)droppedEventCount);
        }

        return writePosition;
    }

    internal static int WriteEventAttributes(ref byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ActivityEvent evnt, ref int dirtyLength)
    {
        var maxAttributeCount = sdkLimitOptions.SpanEventAttributeCountLimit ?? int.MaxValue;
        var maxAttributeValueLength = sdkLimitOptions.SpanAttributeValueLengthLimit ?? int.MaxValue;

        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
            DirtyLength = dirtyLength,
            TagCount = 0,
            DroppedTagCount = 0,
        };

        foreach (ref readonly var tag in evnt.EnumerateTagObjects())
        {
            if (otlpTagWriterState.TagCount < maxAttributeCount)
            {
                if (ProtobufOtlpTagWriter.WriteKeyValue(
                    ref otlpTagWriterState,
                    ProtobufOtlpTraceFieldNumberConstants.Event_Attributes,
                    tag.Key,
                    tag.Value,
                    maxAttributeValueLength))
                {
                    otlpTagWriterState.TagCount++;
                }
                else
                {
                    otlpTagWriterState.DroppedTagCount++;
                }
            }
            else
            {
                otlpTagWriterState.DroppedTagCount++;
            }
        }

        if (otlpTagWriterState.DroppedTagCount > 0)
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpTraceFieldNumberConstants.Event_Dropped_Attributes_Count, ProtobufWireType.VARINT);
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteVarInt32(buffer, otlpTagWriterState.WritePosition, (uint)otlpTagWriterState.DroppedTagCount);
        }

        dirtyLength = otlpTagWriterState.DirtyLength;
        return otlpTagWriterState.WritePosition;
    }

    internal static int WriteSpanLinks(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, Activity activity, ref int dirtyLength)
    {
        var maxLinksCount = sdkLimitOptions.SpanLinkCountLimit ?? int.MaxValue;
        var linkCount = 0;
        var droppedLinkCount = 0;

        foreach (ref readonly var link in activity.EnumerateLinks())
        {
            if (linkCount < maxLinksCount)
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Links, ProtobufWireType.LEN);
                var spanLinksLengthPosition = writePosition;
                writePosition += ProtobufSerializer.ReserveSizeForShortLength;

                writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, TraceIdSize, ProtobufOtlpTraceFieldNumberConstants.Link_Trace_Id, ProtobufWireType.LEN);
                writePosition = WriteTraceId(buffer, writePosition, link.Context.TraceId);
                writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, SpanIdSize, ProtobufOtlpTraceFieldNumberConstants.Link_Span_Id, ProtobufWireType.LEN);
                writePosition = WriteSpanId(buffer, writePosition, link.Context.SpanId);
                if (link.Context.TraceState != null)
                {
                    writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Link_Trace_State, link.Context.TraceState);
                }

                writePosition = WriteLinkAttributes(buffer, writePosition, sdkLimitOptions, link, ref dirtyLength);
                writePosition = WriteTraceFlags(buffer, writePosition, link.Context.TraceFlags, link.Context.IsRemote, ProtobufOtlpTraceFieldNumberConstants.Link_Flags);

                writePosition = ProtobufSerializer.WriteShortReservedLength(buffer, spanLinksLengthPosition, writePosition);
                linkCount++;
            }
            else
            {
                droppedLinkCount++;
            }
        }

        if (droppedLinkCount > 0)
        {
            writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Dropped_Links_Count, ProtobufWireType.VARINT);
            writePosition = ProtobufSerializer.WriteVarInt32(buffer, writePosition, (uint)droppedLinkCount);
        }

        return writePosition;
    }

    internal static int WriteLinkAttributes(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ActivityLink link, ref int dirtyLength)
    {
        var maxAttributeCount = sdkLimitOptions.SpanLinkAttributeCountLimit ?? int.MaxValue;
        var maxAttributeValueLength = sdkLimitOptions.SpanAttributeValueLengthLimit ?? int.MaxValue;
        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
            DirtyLength = dirtyLength,
            TagCount = 0,
            DroppedTagCount = 0,
        };

        foreach (ref readonly var tag in link.EnumerateTagObjects())
        {
            if (otlpTagWriterState.TagCount < maxAttributeCount)
            {
                if (ProtobufOtlpTagWriter.WriteKeyValue(
                    ref otlpTagWriterState,
                    ProtobufOtlpTraceFieldNumberConstants.Link_Attributes,
                    tag.Key,
                    tag.Value,
                    maxAttributeValueLength))
                {
                    otlpTagWriterState.TagCount++;
                }
                else
                {
                    otlpTagWriterState.DroppedTagCount++;
                }
            }
            else
            {
                otlpTagWriterState.DroppedTagCount++;
            }
        }

        if (otlpTagWriterState.DroppedTagCount > 0)
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpTraceFieldNumberConstants.Link_Dropped_Attributes_Count, ProtobufWireType.VARINT);
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteVarInt32(buffer, otlpTagWriterState.WritePosition, (uint)otlpTagWriterState.DroppedTagCount);
        }

        dirtyLength = otlpTagWriterState.DirtyLength;
        return otlpTagWriterState.WritePosition;
    }

    internal static int WriteSpanStatus(byte[] buffer, int position, SdkLimitOptions sdkLimitOptions, Activity activity, StatusCode? statusCode, string? statusMessage)
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
            // The description is free-form text (it may also come from the
            // otel.status_description tag which is skipped by WriteActivityTags)
            // so it is truncated like any other string attribute value of the span.
            var maxAttributeValueLength = sdkLimitOptions.SpanAttributeValueLengthLimit ?? int.MaxValue;
            var message = description.Length > maxAttributeValueLength
                ? description.AsSpan(0, maxAttributeValueLength)
                : description.AsSpan();

            var numberOfUtf8CharsInString = ProtobufSerializer.GetNumberOfUtf8CharsInString(message);
            var serializedLengthSize = ProtobufSerializer.ComputeVarInt64Size((ulong)numberOfUtf8CharsInString);

            // length = numberOfUtf8CharsInString + Status_Message tag size + serializedLengthSize field size + Span_Status tag size + Span_Status length size.
            position = ProtobufSerializer.WriteTagAndLength(buffer, position, numberOfUtf8CharsInString + 1 + serializedLengthSize + 2, ProtobufOtlpTraceFieldNumberConstants.Span_Status, ProtobufWireType.LEN);
            position = ProtobufSerializer.WriteStringWithTag(buffer, position, ProtobufOtlpTraceFieldNumberConstants.Status_Message, numberOfUtf8CharsInString, message);
        }
        else
        {
            position = ProtobufSerializer.WriteTagAndLength(buffer, position, 2, ProtobufOtlpTraceFieldNumberConstants.Span_Status, ProtobufWireType.LEN);
        }

        var finalStatusCode = useActivity ? (int)activity.Status : (statusCode is not null and not StatusCode.Unset) ? (int)statusCode : (int)StatusCode.Unset;
        position = ProtobufSerializer.WriteEnumWithTag(buffer, position, ProtobufOtlpTraceFieldNumberConstants.Status_Code, finalStatusCode);

        return position;
    }

#if NET
    private static void DecodeHex(string hex, Span<byte> destination)
    {
        // This optimization can be removed once https://github.com/dotnet/runtime/pull/134135
        // is available in a future version of System.Diagnostics.DiagnosticSource.
        var status = Convert.FromHexString(hex.AsSpan(), destination, out _, out int bytesWritten);
        if (status != System.Buffers.OperationStatus.Done || bytesWritten != destination.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(destination));
        }
    }
#endif

    /// <summary>
    /// Identifies the instrumentation scope spans are grouped by. Activity
    /// sources sharing a name but reporting different versions, schema URLs or
    /// tags are distinct scopes and must be emitted as separate ScopeSpans
    /// entries.
    /// </summary>
    internal readonly struct InstrumentationScope : IEquatable<InstrumentationScope>
    {
        private readonly ActivitySource source;
        private readonly int hashCode;

        public InstrumentationScope(ActivitySource source)
        {
            this.source = source;
            this.hashCode = ComputeHashCode(source);
        }

        public bool Equals(InstrumentationScope other)
        {
            var source = this.source;
            var otherSource = other.source;

            return ReferenceEquals(source, otherSource)
                || (this.hashCode == other.hashCode
                    && string.Equals(source.Name, otherSource.Name, StringComparison.Ordinal)
                    && string.Equals(source.Version, otherSource.Version, StringComparison.Ordinal)
                    && string.Equals(NormalizeSchemaUrl(source.TelemetrySchemaUrl), NormalizeSchemaUrl(otherSource.TelemetrySchemaUrl), StringComparison.Ordinal)
                    && TagsEqual(source.Tags, otherSource.Tags));
        }

        public override bool Equals(object? obj)
            => obj is InstrumentationScope other && this.Equals(other);

        public override int GetHashCode()
            => this.hashCode;

        private static int ComputeHashCode(ActivitySource source)
        {
            var schemaUrl = NormalizeSchemaUrl(source.TelemetrySchemaUrl);

#if NET || NETSTANDARD2_1_OR_GREATER
            HashCode hashCode = default;
            hashCode.Add(source.Name, StringComparer.Ordinal);
            hashCode.Add(source.Version, StringComparer.Ordinal);
            hashCode.Add(schemaUrl, StringComparer.Ordinal);

            if (source.Tags is IReadOnlyList<KeyValuePair<string, object?>> tags)
            {
                for (var i = 0; i < tags.Count; i++)
                {
                    hashCode.Add(tags[i].Key, StringComparer.Ordinal);
                    hashCode.Add(tags[i].Value);
                }
            }
            else if (source.Tags != null)
            {
                foreach (var tag in source.Tags)
                {
                    hashCode.Add(tag.Key, StringComparer.Ordinal);
                    hashCode.Add(tag.Value);
                }
            }

            return hashCode.ToHashCode();
#else
            unchecked
            {
                var hash = 17;
                hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(source.Name);
                hash = (hash * 31) + (source.Version is null ? 0 : StringComparer.Ordinal.GetHashCode(source.Version));
                hash = (hash * 31) + (schemaUrl is null ? 0 : StringComparer.Ordinal.GetHashCode(schemaUrl));

                if (source.Tags is IReadOnlyList<KeyValuePair<string, object?>> tags)
                {
                    for (var i = 0; i < tags.Count; i++)
                    {
                        hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(tags[i].Key);
                        hash = (hash * 31) + (tags[i].Value?.GetHashCode() ?? 0);
                    }
                }
                else if (source.Tags != null)
                {
                    foreach (var tag in source.Tags)
                    {
                        hash = (hash * 31) + StringComparer.Ordinal.GetHashCode(tag.Key);
                        hash = (hash * 31) + (tag.Value?.GetHashCode() ?? 0);
                    }
                }

                return hash;
            }
#endif
        }

        // Note: A null and an empty schema URL are both exported as no schema URL.
        private static string? NormalizeSchemaUrl(string? schemaUrl)
            => string.IsNullOrEmpty(schemaUrl) ? null : schemaUrl;

        // Note: ActivitySource stores its tags sorted by key, so tags passed in a
        // different order compare as equal. A source without tags and a source
        // with an empty set of tags are both exported without scope attributes.
        private static bool TagsEqual(IEnumerable<KeyValuePair<string, object?>>? tags, IEnumerable<KeyValuePair<string, object?>>? otherTags)
        {
            if (ReferenceEquals(tags, otherTags))
            {
                return true;
            }

            if (tags is IReadOnlyList<KeyValuePair<string, object?>> tagsList
                && otherTags is IReadOnlyList<KeyValuePair<string, object?>> otherTagsList)
            {
                if (tagsList.Count != otherTagsList.Count)
                {
                    return false;
                }

                for (var i = 0; i < tagsList.Count; i++)
                {
                    if (!TagEquals(tagsList[i], otherTagsList[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            using var enumerator = (tags ?? []).GetEnumerator();
            using var otherEnumerator = (otherTags ?? []).GetEnumerator();

            while (true)
            {
                var hasNext = enumerator.MoveNext();
                if (hasNext != otherEnumerator.MoveNext())
                {
                    return false;
                }

                if (!hasNext)
                {
                    return true;
                }

                if (!TagEquals(enumerator.Current, otherEnumerator.Current))
                {
                    return false;
                }
            }
        }

        // Note: Mirrors the tag equality the SDK uses for metric streams.
        private static bool TagEquals(KeyValuePair<string, object?> tag, KeyValuePair<string, object?> otherTag)
            => string.Equals(tag.Key, otherTag.Key, StringComparison.Ordinal)
            && (ReferenceEquals(tag.Value, otherTag.Value) || (tag.Value?.Equals(otherTag.Value) ?? otherTag.Value == null));
    }
}
