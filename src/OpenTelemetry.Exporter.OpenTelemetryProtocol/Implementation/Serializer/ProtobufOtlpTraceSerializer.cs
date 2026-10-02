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

    // Totals of items discarded due to span limits, accumulated while a batch
    // is serialized so that at most one message is logged per batch.
    [ThreadStatic]
    private static int spansWithDroppedItemsCount;
    [ThreadStatic]
    private static long spanDroppedAttributeCount;
    [ThreadStatic]
    private static long spanDroppedEventCount;
    [ThreadStatic]
    private static long spanDroppedLinkCount;

    internal static int WriteTraceData(
        ref byte[] buffer,
        int writePosition,
        OtlpSpanLimits otlpSpanLimits,
        Resources.Resource? resource,
        in Batch<Activity> batch,
        int maxBufferSize = ProtobufSerializer.MaxBufferSize)
    {
        activityListPool ??= [];
        scopeTracesList ??= [];

        // Note: The grouped batch is held in thread-static state, so it has to be
        // released even when serialization fails. TryWriteResourceSpans rethrows
        // once the buffer cannot be grown any further; leaving the batch behind
        // would merge it into the next export on this thread.
        try
        {
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

            writePosition = TryWriteResourceSpans(ref buffer, writePosition, otlpSpanLimits, resource, maxBufferSize);
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
        OtlpSpanLimits otlpSpanLimits,
        Resources.Resource? resource,
        int maxBufferSize = ProtobufSerializer.MaxBufferSize)
    {
        while (true)
        {
            var entryWritePosition = writePosition;

            // A retry with a larger buffer serializes every span again,
            // so the totals restart with each attempt.
            ResetSpanLimitDropTotals();

            try
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.TracesData_Resource_Spans, ProtobufWireType.LEN);
                var resourceSpansScopeSpansLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;

                writePosition = WriteResourceSpans(buffer, writePosition, otlpSpanLimits, resource);

                ProtobufSerializer.WriteReservedLength(buffer, resourceSpansScopeSpansLengthPosition, writePosition - (resourceSpansScopeSpansLengthPosition + ReserveSizeForLength));

                otlpSpanLimits.WarningTracker.RecordAndWarnIfDue(
                    new(spansWithDroppedItemsCount, spanDroppedAttributeCount, spanDroppedEventCount, spanDroppedLinkCount),
                    static totals => OpenTelemetryProtocolExporterEventSource.Log.SpanLimitsExceeded(
                        totals.AffectedItemCount,
                        totals.DroppedAttributeCount,
                        totals.DroppedEventCount,
                        totals.DroppedLinkCount));

                // Serialization succeeded, return the final write position
                return writePosition;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                // Reset write position and attempt to increase the buffer size
                writePosition = entryWritePosition;

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
    }

    internal static int WriteResourceSpans(byte[] buffer, int writePosition, OtlpSpanLimits otlpSpanLimits, Resources.Resource? resource)
    {
        writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, writePosition, resource);
        writePosition = WriteScopeSpans(buffer, writePosition, otlpSpanLimits);

        if (resource?.SchemaUrl is { Length: > 0 } schemaUrl)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ResourceSpans_Schema_Url, schemaUrl);
        }

        return writePosition;
    }

    internal static int WriteScopeSpans(byte[] buffer, int writePosition, OtlpSpanLimits otlpSpanLimits)
    {
        if (scopeTracesList != null)
        {
            foreach (var entry in scopeTracesList)
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ResourceSpans_Scope_Spans, ProtobufWireType.LEN);
                var resourceSpansScopeSpansLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;

                writePosition = WriteScopeSpan(buffer, writePosition, otlpSpanLimits, entry.Value[0].Source, entry.Value);
                ProtobufSerializer.WriteReservedLength(buffer, resourceSpansScopeSpansLengthPosition, writePosition - (resourceSpansScopeSpansLengthPosition + ReserveSizeForLength));
            }
        }

        return writePosition;
    }

    internal static int WriteScopeSpan(byte[] buffer, int writePosition, OtlpSpanLimits otlpSpanLimits, ActivitySource activitySource, List<Activity> activities)
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
            var maxAttributeCount = otlpSpanLimits.ScopeAttributeCountLimit;
            var maxAttributeValueLength = otlpSpanLimits.ScopeAttributeValueLengthLimit ?? int.MaxValue;
            var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
            {
                Buffer = buffer,
                WritePosition = writePosition,
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
        }

        ProtobufSerializer.WriteReservedLength(buffer, instrumentationScopeLengthPosition, writePosition - (instrumentationScopeLengthPosition + ReserveSizeForLength));

        for (var i = 0; i < activities.Count; i++)
        {
            writePosition = WriteSpan(buffer, writePosition, otlpSpanLimits, activities[i]);
        }

        if (!string.IsNullOrEmpty(activitySource.TelemetrySchemaUrl))
        {
#pragma warning disable IDE0370 // Suppression is unnecessary
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ScopeSpans_Schema_Url, activitySource.TelemetrySchemaUrl!);
#pragma warning restore IDE0370 // Suppression is unnecessary
        }

        return writePosition;
    }

    internal static int WriteSpan(byte[] buffer, int writePosition, OtlpSpanLimits otlpSpanLimits, Activity activity)
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

        (writePosition, var statusCode, var statusMessage, var droppedAttributeCount) = WriteActivityTags(buffer, writePosition, otlpSpanLimits, activity);
        writePosition = WriteSpanEvents(buffer, writePosition, otlpSpanLimits, activity, ref droppedAttributeCount, out var droppedEventCount);
        writePosition = WriteSpanLinks(buffer, writePosition, otlpSpanLimits, activity, ref droppedAttributeCount, out var droppedLinkCount);
        writePosition = WriteSpanStatus(buffer, writePosition, activity, statusCode, statusMessage);
        writePosition = ProtobufSerializer.WriteShortReservedLength(buffer, spanLengthPosition, writePosition);

        if (droppedAttributeCount > 0 || droppedEventCount > 0 || droppedLinkCount > 0)
        {
            spansWithDroppedItemsCount++;
            spanDroppedAttributeCount += droppedAttributeCount;
            spanDroppedEventCount += droppedEventCount;
            spanDroppedLinkCount += droppedLinkCount;
        }

        return writePosition;
    }

    internal static int WriteTraceId(byte[] buffer, int position, ActivityTraceId activityTraceId)
    {
        var traceBytes = new Span<byte>(buffer, position, TraceIdSize);
#if NET9_0_OR_GREATER
        DecodeHex(activityTraceId.ToHexString(), traceBytes);
#else
        activityTraceId.CopyTo(traceBytes);
#endif
        return position + TraceIdSize;
    }

    internal static int WriteSpanId(byte[] buffer, int position, ActivitySpanId activitySpanId)
    {
        var spanIdBytes = new Span<byte>(buffer, position, SpanIdSize);
#if NET9_0_OR_GREATER
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

    internal static (int Position, StatusCode? StatusCode, string? StatusMessage, int DroppedAttributeCount) WriteActivityTags(byte[] buffer, int writePosition, OtlpSpanLimits otlpSpanLimits, Activity activity)
    {
        StatusCode? statusCode = null;
        string? statusMessage = null;
        var maxAttributeCount = otlpSpanLimits.AttributeCountLimit;
        var maxAttributeValueLength = otlpSpanLimits.SpanAttributeValueLengthLimit ?? int.MaxValue;
        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
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

        return (otlpTagWriterState.WritePosition, statusCode, statusMessage, otlpTagWriterState.DroppedTagCount);
    }

    internal static int WriteSpanEvents(byte[] buffer, int writePosition, OtlpSpanLimits otlpSpanLimits, Activity activity, ref int droppedAttributeCount, out int droppedEventCount)
    {
        var maxEventCountLimit = otlpSpanLimits.EventCountLimit;
        var eventCount = 0;
        droppedEventCount = 0;
        foreach (ref readonly var evnt in activity.EnumerateEvents())
        {
            if (eventCount < maxEventCountLimit)
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Events, ProtobufWireType.LEN);
                var spanEventsLengthPosition = writePosition;
                writePosition += ProtobufSerializer.ReserveSizeForShortLength;

                writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Event_Name, evnt.Name);
                writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Event_Time_Unix_Nano, (ulong)evnt.Timestamp.ToUnixTimeNanoseconds());
                writePosition = WriteEventAttributes(ref buffer, writePosition, otlpSpanLimits, evnt, ref droppedAttributeCount);

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

    internal static int WriteEventAttributes(ref byte[] buffer, int writePosition, OtlpSpanLimits otlpSpanLimits, ActivityEvent evnt, ref int droppedAttributeCount)
    {
        var maxAttributeCount = otlpSpanLimits.AttributePerEventCountLimit;
        var maxAttributeValueLength = otlpSpanLimits.SpanAttributeValueLengthLimit ?? int.MaxValue;

        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
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
            droppedAttributeCount += otlpTagWriterState.DroppedTagCount;
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpTraceFieldNumberConstants.Event_Dropped_Attributes_Count, ProtobufWireType.VARINT);
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteVarInt32(buffer, otlpTagWriterState.WritePosition, (uint)otlpTagWriterState.DroppedTagCount);
        }

        return otlpTagWriterState.WritePosition;
    }

    internal static int WriteSpanLinks(byte[] buffer, int writePosition, OtlpSpanLimits otlpSpanLimits, Activity activity, ref int droppedAttributeCount, out int droppedLinkCount)
    {
        var maxLinksCount = otlpSpanLimits.LinkCountLimit;
        var linkCount = 0;
        droppedLinkCount = 0;

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

                writePosition = WriteLinkAttributes(buffer, writePosition, otlpSpanLimits, link, ref droppedAttributeCount);
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

    internal static int WriteLinkAttributes(byte[] buffer, int writePosition, OtlpSpanLimits otlpSpanLimits, ActivityLink link, ref int droppedAttributeCount)
    {
        var maxAttributeCount = otlpSpanLimits.AttributePerLinkCountLimit;
        var maxAttributeValueLength = otlpSpanLimits.SpanAttributeValueLengthLimit ?? int.MaxValue;
        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
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
            droppedAttributeCount += otlpTagWriterState.DroppedTagCount;
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpTraceFieldNumberConstants.Link_Dropped_Attributes_Count, ProtobufWireType.VARINT);
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteVarInt32(buffer, otlpTagWriterState.WritePosition, (uint)otlpTagWriterState.DroppedTagCount);
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
            var numberOfUtf8CharsInString = ProtobufSerializer.GetNumberOfUtf8CharsInString(description);
            var serializedLengthSize = ProtobufSerializer.ComputeVarInt64Size((ulong)numberOfUtf8CharsInString);

            // length = numberOfUtf8CharsInString + Status_Message tag size + serializedLengthSize field size + Span_Status tag size + Span_Status length size.
            position = ProtobufSerializer.WriteTagAndLength(buffer, position, numberOfUtf8CharsInString + 1 + serializedLengthSize + 2, ProtobufOtlpTraceFieldNumberConstants.Span_Status, ProtobufWireType.LEN);
            position = ProtobufSerializer.WriteStringWithTag(buffer, position, ProtobufOtlpTraceFieldNumberConstants.Status_Message, numberOfUtf8CharsInString, description);
        }
        else
        {
            position = ProtobufSerializer.WriteTagAndLength(buffer, position, 2, ProtobufOtlpTraceFieldNumberConstants.Span_Status, ProtobufWireType.LEN);
        }

        var finalStatusCode = useActivity ? (int)activity.Status : (statusCode is not null and not StatusCode.Unset) ? (int)statusCode : (int)StatusCode.Unset;
        position = ProtobufSerializer.WriteEnumWithTag(buffer, position, ProtobufOtlpTraceFieldNumberConstants.Status_Code, finalStatusCode);

        return position;
    }

#if NET9_0_OR_GREATER
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

    private static void ResetSpanLimitDropTotals()
    {
        spansWithDroppedItemsCount = 0;
        spanDroppedAttributeCount = 0;
        spanDroppedEventCount = 0;
        spanDroppedLinkCount = 0;
    }
}
