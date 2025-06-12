// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpLogSerializer
{
    private const int ReserveSizeForLength = 4;
    private const int TraceIdSize = 16;
    private const int SpanIdSize = 8;

    [ThreadStatic]
    private static Stack<List<LogRecord>>? logsListPool;
    [ThreadStatic]
    private static Dictionary<string, List<LogRecord>>? scopeLogsList;

    [ThreadStatic]
    private static SerializationState? threadSerializationState;

    internal static int WriteLogsData(ref byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions, Resources.Resource? resource, in Batch<LogRecord> logRecordBatch)
    {
        logsListPool ??= [];
        scopeLogsList ??= [];

        foreach (var logRecord in logRecordBatch)
        {
            var scopeName = logRecord.Logger.Name;
            if (!scopeLogsList.TryGetValue(scopeName, out var logRecords))
            {
                logRecords = logsListPool.Count > 0 ? logsListPool.Pop() : [];
                scopeLogsList[scopeName] = logRecords;
            }

            if (logRecord.Source == LogRecord.LogRecordSource.FromSharedPool)
            {
                Debug.Assert(logRecord.PoolReferenceCount > 0, "logRecord PoolReferenceCount value was unexpected");

                // Note: AddReference call here prevents the LogRecord from
                // being given back to the pool by Batch<LogRecord>.
                logRecord.AddReference();
            }

            logRecords.Add(logRecord);
        }

        writePosition = TryWriteResourceLogs(ref buffer, writePosition, sdkLimitOptions, experimentalOptions, resource, scopeLogsList);
        ReturnLogRecordListToPool();

        return writePosition;
    }

    internal static int TryWriteResourceLogs(ref byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions, Resources.Resource? resource, Dictionary<string, List<LogRecord>> scopeLogs)
    {
        int entryWritePosition = writePosition;

        try
        {
            writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpLogFieldNumberConstants.LogsData_Resource_Logs, ProtobufWireType.LEN);
            int logsDataLengthPosition = writePosition;
            writePosition += ReserveSizeForLength;

            writePosition = WriteResourceLogs(buffer, writePosition, sdkLimitOptions, experimentalOptions, resource, scopeLogs);

            ProtobufSerializer.WriteReservedLength(buffer, logsDataLengthPosition, writePosition - (logsDataLengthPosition + ReserveSizeForLength));
        }
        catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentException)
        {
            writePosition = entryWritePosition;
            if (!ProtobufSerializer.IncreaseBufferSize(ref buffer, OtlpSignalType.Logs))
            {
                throw;
            }

            return TryWriteResourceLogs(ref buffer, writePosition, sdkLimitOptions, experimentalOptions, resource, scopeLogs);
        }

        return writePosition;
    }

    internal static void ReturnLogRecordListToPool()
    {
        if (scopeLogsList?.Count != 0)
        {
            foreach (var entry in scopeLogsList!)
            {
                foreach (var logRecord in entry.Value)
                {
                    if (logRecord.Source == LogRecord.LogRecordSource.FromSharedPool)
                    {
                        Debug.Assert(logRecord.PoolReferenceCount > 0, "logRecord PoolReferenceCount value was unexpected");

                        // Note: Try to return the LogRecord to the shared pool
                        // now that work is done.
                        LogRecordSharedPool.Current.Return(logRecord);
                    }
                }

                entry.Value.Clear();
                logsListPool?.Push(entry.Value);
            }

            scopeLogsList.Clear();
        }
    }

    internal static int WriteResourceLogs(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions, Resources.Resource? resource, Dictionary<string, List<LogRecord>> scopeLogs)
    {
        writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, writePosition, resource);
        writePosition = WriteScopeLogs(buffer, writePosition, sdkLimitOptions, experimentalOptions, scopeLogs);
        return writePosition;
    }

    internal static int WriteScopeLogs(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions, Dictionary<string, List<LogRecord>> scopeLogs)
    {
        if (scopeLogs != null)
        {
            foreach (KeyValuePair<string, List<LogRecord>> entry in scopeLogs)
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpLogFieldNumberConstants.ResourceLogs_Scope_Logs, ProtobufWireType.LEN);
                int resourceLogsScopeLogsLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;

                writePosition = WriteScopeLog(buffer, writePosition, sdkLimitOptions, experimentalOptions, entry.Value[0].Logger.Name, entry.Value);
                ProtobufSerializer.WriteReservedLength(buffer, resourceLogsScopeLogsLengthPosition, writePosition - (resourceLogsScopeLogsLengthPosition + ReserveSizeForLength));
            }
        }

        return writePosition;
    }

    internal static int WriteScopeLog(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions, string loggerName, List<LogRecord> logRecords)
    {
        var value = loggerName.AsSpan();
        var numberOfUtf8CharsInString = ProtobufSerializer.GetNumberOfUtf8CharsInString(value);
        var serializedLengthSize = ProtobufSerializer.ComputeVarInt64Size((ulong)numberOfUtf8CharsInString);

        // numberOfUtf8CharsInString + tagSize + length field size.
        writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, numberOfUtf8CharsInString + 1 + serializedLengthSize, ProtobufOtlpLogFieldNumberConstants.ScopeLogs_Scope, ProtobufWireType.LEN);
        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Name, numberOfUtf8CharsInString, value);

        for (int i = 0; i < logRecords.Count; i++)
        {
            writePosition = WriteLogRecord(buffer, writePosition, sdkLimitOptions, experimentalOptions, logRecords[i]);
        }

        return writePosition;
    }

    internal static int WriteLogRecord(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions, LogRecord logRecord)
    {
        var state = threadSerializationState ??= new();

        state.AttributeValueLengthLimit = sdkLimitOptions.LogRecordAttributeValueLengthLimit;
        state.AttributeCountLimit = sdkLimitOptions.LogRecordAttributeCountLimit ?? int.MaxValue;
        state.TagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
            TagCount = 0,
            DroppedTagCount = 0,
        };

        ref var otlpTagWriterState = ref state.TagWriterState;

        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.ScopeLogs_Log_Records, ProtobufWireType.LEN);
        int logRecordLengthPosition = otlpTagWriterState.WritePosition;
        otlpTagWriterState.WritePosition += ReserveSizeForLength;

        var timestamp = (ulong)logRecord.Timestamp.ToUnixTimeNanoseconds();
        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteFixed64WithTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Time_Unix_Nano, timestamp);
        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteFixed64WithTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Observed_Time_Unix_Nano, timestamp);

        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteEnumWithTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Severity_Number, logRecord.Severity.HasValue ? (int)logRecord.Severity : 0);

        if (!string.IsNullOrWhiteSpace(logRecord.SeverityText))
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteStringWithTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Severity_Text, logRecord.SeverityText!);
        }
        else if (logRecord.Severity.HasValue)
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteStringWithTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Severity_Text, logRecord.Severity.Value.ToShortName());
        }

        if (experimentalOptions.EmitLogEventAttributes)
        {
            if (logRecord.EventId.Id != default)
            {
                AddLogAttribute(state, ExperimentalOptions.LogRecordEventIdAttribute, logRecord.EventId.Id);
            }
        }

        if (logRecord.Exception != null)
        {
            AddLogAttribute(state, SemanticConventions.AttributeExceptionType, logRecord.Exception.GetType().Name);
            AddLogAttribute(state, SemanticConventions.AttributeExceptionMessage, logRecord.Exception.Message);
            AddLogAttribute(state, SemanticConventions.AttributeExceptionStacktrace, logRecord.Exception.ToInvariantString());
        }

        bool bodyPopulatedFromFormattedMessage = false;
        bool isLogRecordBodySet = false;

        if (logRecord.FormattedMessage != null)
        {
            otlpTagWriterState.WritePosition = WriteLogRecordBody(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, logRecord.FormattedMessage.AsSpan());
            bodyPopulatedFromFormattedMessage = true;
            isLogRecordBodySet = true;
        }

        if (logRecord.Attributes != null)
        {
            foreach (var attribute in logRecord.Attributes)
            {
                // Special casing {OriginalFormat}
                // See https://github.com/open-telemetry/opentelemetry-dotnet/pull/3182
                // for explanation.
                if (attribute.Key.Equals("{OriginalFormat}", StringComparison.Ordinal) && !bodyPopulatedFromFormattedMessage)
                {
                    otlpTagWriterState.WritePosition = WriteLogRecordBody(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, (attribute.Value as string).AsSpan());
                    isLogRecordBodySet = true;
                }
                else
                {
                    AddLogAttribute(state, attribute);
                }
            }

            // Supports setting Body directly on LogRecord for the Logs Bridge API.
            if (!isLogRecordBodySet && logRecord.Body != null)
            {
                // If {OriginalFormat} is not present in the attributes,
                // use logRecord.Body if it is set.
                otlpTagWriterState.WritePosition = WriteLogRecordBody(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, logRecord.Body.AsSpan());
            }
        }

        if (logRecord.TraceId != default && logRecord.SpanId != default)
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTagAndLength(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, TraceIdSize, ProtobufOtlpLogFieldNumberConstants.LogRecord_Trace_Id, ProtobufWireType.LEN);
            otlpTagWriterState.WritePosition = ProtobufOtlpTraceSerializer.WriteTraceId(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, logRecord.TraceId);

            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTagAndLength(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, SpanIdSize, ProtobufOtlpLogFieldNumberConstants.LogRecord_Span_Id, ProtobufWireType.LEN);
            otlpTagWriterState.WritePosition = ProtobufOtlpTraceSerializer.WriteSpanId(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, logRecord.SpanId);

            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteFixed32WithTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Flags, (uint)logRecord.TraceFlags);
        }

        if (logRecord.EventId.Name != null)
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteStringWithTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Event_Name, logRecord.EventId.Name!);
        }

        logRecord.ForEachScope(ProcessScope, state);

        if (otlpTagWriterState.DroppedTagCount > 0)
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Dropped_Attributes_Count, ProtobufWireType.VARINT);
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteVarInt32(buffer, otlpTagWriterState.WritePosition, (uint)otlpTagWriterState.DroppedTagCount);
        }

        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer, logRecordLengthPosition, otlpTagWriterState.WritePosition - (logRecordLengthPosition + ReserveSizeForLength));

        return otlpTagWriterState.WritePosition;

        static void ProcessScope(LogRecordScope scope, SerializationState state)
        {
            foreach (var scopeItem in scope)
            {
                if (scopeItem.Key.Equals("{OriginalFormat}", StringComparison.Ordinal) || string.IsNullOrEmpty(scopeItem.Key))
                {
                    // Ignore if the scope key is empty.
                    // Ignore if the scope key is {OriginalFormat}
                    // Attributes should not contain duplicates,
                    // and it is expensive to de-dup, so this
                    // exporter is going to pass the scope items as is.
                    // {OriginalFormat} is going to be the key
                    // if one uses formatted string for scopes
                    // and if there are nested scopes, this is
                    // guaranteed to create duplicate keys.
                    // Similar for empty keys, which is what the
                    // key is going to be if user simply
                    // passes a string as scope.
                    // To summarize this exporter only allows
                    // IReadOnlyList<KeyValuePair<string, object?>>
                    // or IEnumerable<KeyValuePair<string, object?>>.
                    // and expect users to provide unique keys.
                    // Note: It is possible that we allow users
                    // to override this exporter feature. So not blocking
                    // empty/{OriginalFormat} in the SDK itself.
                }
                else
                {
                    AddLogAttribute(state, scopeItem);
                }
            }
        }
    }

    private static int WriteLogRecordBody(byte[] buffer, int writePosition, ReadOnlySpan<char> value)
    {
        var numberOfUtf8CharsInString = ProtobufSerializer.GetNumberOfUtf8CharsInString(value);
        var serializedLengthSize = ProtobufSerializer.ComputeVarInt64Size((ulong)numberOfUtf8CharsInString);

        // length = numberOfUtf8CharsInString + tagSize + length field size.
        writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, numberOfUtf8CharsInString + 1 + serializedLengthSize, ProtobufOtlpLogFieldNumberConstants.LogRecord_Body, ProtobufWireType.LEN);
        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpCommonFieldNumberConstants.AnyValue_String_Value, numberOfUtf8CharsInString, value);
        return writePosition;
    }

    private static void AddLogAttribute(SerializationState state, KeyValuePair<string, object?> attribute)
    {
        AddLogAttribute(state, attribute.Key, attribute.Value);
    }

    private static void AddLogAttribute(SerializationState state, string key, object? value)
    {
        if (state.TagWriterState.TagCount == state.AttributeCountLimit)
        {
            state.TagWriterState.DroppedTagCount++;
        }
        else
        {
            state.TagWriterState.WritePosition = ProtobufSerializer.WriteTag(state.TagWriterState.Buffer, state.TagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Attributes, ProtobufWireType.LEN);
            int logAttributesLengthPosition = state.TagWriterState.WritePosition;
            state.TagWriterState.WritePosition += ReserveSizeForLength;

            ProtobufOtlpTagWriter.Instance.TryWriteTag(ref state.TagWriterState, key, value, state.AttributeValueLengthLimit);

            var logAttributesLength = state.TagWriterState.WritePosition - (logAttributesLengthPosition + ReserveSizeForLength);
            ProtobufSerializer.WriteReservedLength(state.TagWriterState.Buffer, logAttributesLengthPosition, logAttributesLength);
            state.TagWriterState.TagCount++;
        }
    }

    private sealed class SerializationState
    {
        public int? AttributeValueLengthLimit;
        public int AttributeCountLimit;
        public ProtobufOtlpTagWriter.OtlpTagWriterState TagWriterState;
    }
}
