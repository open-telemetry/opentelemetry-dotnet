// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpTraceSerializer
{
    private const int ReserveSizeForLength = 4;
    private const int TraceIdSize = 16;
    private const int SpanIdSize = 8;

    [ThreadStatic]
    private static Stack<List<Activity>>? activityListPool;
    [ThreadStatic]
    private static Dictionary<string, List<Activity>>? scopeTracesList;

    // Opens a batch for this call only, so drops are reported as soon as serialization succeeds.
    // OtlpTraceExporter opens the batch itself, so it can discard the drops when it does not send
    // the request.
    internal static int WriteTraceData(
        ref byte[] buffer,
        int writePosition,
        SpanLimiter spanLimiter,
        Resources.Resource? resource,
        in Batch<Activity> batch,
        int maxBufferSize = ProtobufSerializer.MaxBufferSize)
    {
        using var spanLimitBatch = spanLimiter.BeginBatch();

        return WriteTraceData(ref buffer, writePosition, spanLimitBatch, resource, batch, maxBufferSize);
    }

    internal static int WriteTraceData(
        ref byte[] buffer,
        int writePosition,
        SpanLimitBatch spanLimitBatch,
        Resources.Resource? resource,
        in Batch<Activity> batch,
        int maxBufferSize = ProtobufSerializer.MaxBufferSize)
    {
        activityListPool ??= [];
        scopeTracesList ??= [];

        // Note:The grouped batch is held in thread-static state, so it has to be
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

            writePosition = TryWriteResourceSpans(ref buffer, writePosition, spanLimitBatch, resource, maxBufferSize);
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
        SpanLimitBatch spanLimitBatch,
        Resources.Resource? resource,
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

                writePosition = WriteResourceSpans(buffer, writePosition, spanLimitBatch, resource);

                ProtobufSerializer.WriteReservedLength(buffer, resourceSpansScopeSpansLengthPosition, writePosition - (resourceSpansScopeSpansLengthPosition + ReserveSizeForLength));

                // Serialization succeeded, return the final write position
                return writePosition;
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                // Every span is read again on retry, so the drops recorded so far are discarded.
                // If the buffer cannot grow, the batch is dropped and no drops are reported.
                spanLimitBatch.Reset();

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

    internal static int WriteResourceSpans(byte[] buffer, int writePosition, SpanLimitBatch spanLimitBatch, Resources.Resource? resource)
    {
        writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, writePosition, resource);
        writePosition = WriteScopeSpans(buffer, writePosition, spanLimitBatch);

        if (resource?.SchemaUrl is { Length: > 0 } schemaUrl)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ResourceSpans_Schema_Url, schemaUrl);
        }

        return writePosition;
    }

    internal static int WriteScopeSpans(byte[] buffer, int writePosition, SpanLimitBatch spanLimitBatch)
    {
        if (scopeTracesList != null)
        {
            foreach (var entry in scopeTracesList)
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ResourceSpans_Scope_Spans, ProtobufWireType.LEN);
                var resourceSpansScopeSpansLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;

                writePosition = WriteScopeSpan(buffer, writePosition, spanLimitBatch, entry.Value[0].Source, entry.Value);
                ProtobufSerializer.WriteReservedLength(buffer, resourceSpansScopeSpansLengthPosition, writePosition - (resourceSpansScopeSpansLengthPosition + ReserveSizeForLength));
            }
        }

        return writePosition;
    }

    internal static int WriteScopeSpan(byte[] buffer, int writePosition, SpanLimitBatch spanLimitBatch, ActivitySource activitySource, List<Activity> activities)
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
            var maxAttributeCount = spanLimitBatch.ScopeAttributeCountLimit;
            var maxAttributeValueLength = spanLimitBatch.ScopeAttributeValueLengthLimit;
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
            writePosition = WriteSpan(buffer, writePosition, spanLimitBatch, activities[i]);
        }

        if (!string.IsNullOrEmpty(activitySource.TelemetrySchemaUrl))
        {
#pragma warning disable IDE0370 // Suppression is unnecessary
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.ScopeSpans_Schema_Url, activitySource.TelemetrySchemaUrl!);
#pragma warning restore IDE0370 // Suppression is unnecessary
        }

        return writePosition;
    }

    internal static int WriteSpan(byte[] buffer, int writePosition, SpanLimitBatch spanLimitBatch, Activity activity)
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

        var attributes = spanLimitBatch.CreateAttributeReader(activity);
        writePosition = WriteActivityTags(buffer, writePosition, ref attributes);
        writePosition = WriteSpanEvents(buffer, writePosition, spanLimitBatch, activity);
        writePosition = WriteSpanLinks(buffer, writePosition, spanLimitBatch, activity);
        var status = attributes.Status;
        writePosition = WriteSpanStatus(buffer, writePosition, status, status == ActivityStatusCode.Error ? attributes.StatusDescription : null, attributes.ValueLengthLimit);
        writePosition = ProtobufSerializer.WriteShortReservedLength(buffer, spanLengthPosition, writePosition);

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

    internal static int WriteActivityTags(byte[] buffer, int writePosition, ref SpanAttributeReader spanAttributeReader)
    {
        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
        };

        while (spanAttributeReader.TryRead(out var tag))
        {
            if (!ProtobufOtlpTagWriter.WriteKeyValue(
                ref otlpTagWriterState,
                ProtobufOtlpTraceFieldNumberConstants.Span_Attributes,
                tag.Key,
                tag.Value,
                spanAttributeReader.ValueLengthLimit))
            {
                spanAttributeReader.MarkDropped();
            }
        }

        return WriteDroppedCount(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Dropped_Attributes_Count, spanAttributeReader.DroppedCount);
    }

    internal static int WriteSpanEvents(byte[] buffer, int writePosition, SpanLimitBatch spanLimitBatch, Activity activity)
    {
        var events = spanLimitBatch.CreateEventReader(activity);
        while (events.TryRead(out var evnt))
        {
            writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Events, ProtobufWireType.LEN);
            var spanEventsLengthPosition = writePosition;
            writePosition += ProtobufSerializer.ReserveSizeForShortLength;

            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Event_Name, evnt.Name);
            writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Event_Time_Unix_Nano, (ulong)evnt.Timestamp.ToUnixTimeNanoseconds());

            var attributeReader = events.CreateAttributeReader();
            writePosition = WriteEventAttributes(ref buffer, writePosition, ref attributeReader);

            writePosition = ProtobufSerializer.WriteShortReservedLength(buffer, spanEventsLengthPosition, writePosition);
        }

        return WriteDroppedCount(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Dropped_Events_Count, events.DroppedCount);
    }

    internal static int WriteEventAttributes(ref byte[] buffer, int writePosition, ref AttributeReader attributeReader)
    {
        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
        };

        while (attributeReader.TryRead(out var tag))
        {
            if (!ProtobufOtlpTagWriter.WriteKeyValue(
                ref otlpTagWriterState,
                ProtobufOtlpTraceFieldNumberConstants.Event_Attributes,
                tag.Key,
                tag.Value,
                attributeReader.ValueLengthLimit))
            {
                attributeReader.MarkDropped();
            }
        }

        return WriteDroppedCount(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpTraceFieldNumberConstants.Event_Dropped_Attributes_Count, attributeReader.DroppedCount);
    }

    internal static int WriteSpanLinks(byte[] buffer, int writePosition, SpanLimitBatch spanLimitBatch, Activity activity)
    {
        var spanLinkReader = spanLimitBatch.CreateLinkReader(activity);
        while (spanLinkReader.TryRead(out var link))
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

            var attributeReader = spanLinkReader.CreateAttributeReader();
            writePosition = WriteLinkAttributes(buffer, writePosition, ref attributeReader);
            writePosition = WriteTraceFlags(buffer, writePosition, link.Context.TraceFlags, link.Context.IsRemote, ProtobufOtlpTraceFieldNumberConstants.Link_Flags);

            writePosition = ProtobufSerializer.WriteShortReservedLength(buffer, spanLinksLengthPosition, writePosition);
        }

        return WriteDroppedCount(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.Span_Dropped_Links_Count, spanLinkReader.DroppedCount);
    }

    internal static int WriteLinkAttributes(byte[] buffer, int writePosition, ref AttributeReader attributeReader)
    {
        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
        };

        while (attributeReader.TryRead(out var tag))
        {
            if (!ProtobufOtlpTagWriter.WriteKeyValue(
                ref otlpTagWriterState,
                ProtobufOtlpTraceFieldNumberConstants.Link_Attributes,
                tag.Key,
                tag.Value,
                attributeReader.ValueLengthLimit))
            {
                attributeReader.MarkDropped();
            }
        }

        return WriteDroppedCount(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpTraceFieldNumberConstants.Link_Dropped_Attributes_Count, attributeReader.DroppedCount);
    }

    internal static int WriteSpanStatus(byte[] buffer, int position, ActivityStatusCode status, string? description, int? valueLengthLimit)
    {
        // An Unset status is not written. It is the default, so receivers read it as Unset.
        if (status == ActivityStatusCode.Unset)
        {
            return position;
        }

        if (status == ActivityStatusCode.Error && description != null)
        {
            // The description is free-form text (it may also come from the
            // otel.status_description tag which is skipped by WriteActivityTags)
            // so it is truncated like any other string attribute value of the span.
            var maxAttributeValueLength = valueLengthLimit ?? int.MaxValue;
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

        position = ProtobufSerializer.WriteEnumWithTag(buffer, position, ProtobufOtlpTraceFieldNumberConstants.Status_Code, (int)status);

        return position;
    }

    private static int WriteDroppedCount(byte[] buffer, int writePosition, int fieldNumber, int droppedCount)
    {
        if (droppedCount > 0)
        {
            writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.VARINT);
            writePosition = ProtobufSerializer.WriteVarInt32(buffer, writePosition, (uint)droppedCount);
        }

        return writePosition;
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
}
