// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpLogSerializer
{
    private const int ReserveSizeForLength = 4;
    private const int TraceIdSize = 16;
    private const int SpanIdSize = 8;

    private static readonly Stack<List<LogRecord>> LogsListPool = [];
    private static readonly Dictionary<string, List<LogRecord>> ScopeLogsList = [];

    internal static int WriteLogsData(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions, Resources.Resource? resource, in Batch<LogRecord> logRecordBatch)
    {
        foreach (var logRecord in logRecordBatch)
        {
            var scopeName = logRecord.Logger.Name;
            if (!ScopeLogsList.TryGetValue(scopeName, out var logRecords))
            {
                logRecords = LogsListPool.Count > 0 ? LogsListPool.Pop() : [];
                ScopeLogsList[scopeName] = logRecords;
            }

            logRecords.Add(logRecord);
        }

        writePosition = WriteResourceLogs(buffer, writePosition, sdkLimitOptions, experimentalOptions, resource, ScopeLogsList);
        ReturnLogRecordListToPool();

        return writePosition;
    }

    internal static void ReturnLogRecordListToPool()
    {
        if (ScopeLogsList.Count != 0)
        {
            foreach (var entry in ScopeLogsList)
            {
                entry.Value.Clear();
                LogsListPool.Push(entry.Value);
            }

            ScopeLogsList.Clear();
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
        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpLogFieldNumberConstants.InstrumentationScope_Name, numberOfUtf8CharsInString, value);

        for (int i = 0; i < logRecords.Count; i++)
        {
            writePosition = WriteLogRecord(buffer, writePosition, sdkLimitOptions, experimentalOptions, logRecords[i]);
        }

        return writePosition;
    }

    internal static int WriteLogRecord(byte[] buffer, int writePosition, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions, LogRecord logRecord)
    {
        var attributeValueLengthLimit = sdkLimitOptions.LogRecordAttributeValueLengthLimit;
        var attributeCountLimit = sdkLimitOptions.LogRecordAttributeCountLimit ?? int.MaxValue;

        ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
            TagCount = 0,
            DroppedTagCount = 0,
        };

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
                otlpTagWriterState = AddLogAttribute(ref otlpTagWriterState, ExperimentalOptions.LogRecordEventIdAttribute, logRecord.EventId.Id, attributeCountLimit, attributeValueLengthLimit);
            }

            if (!string.IsNullOrEmpty(logRecord.EventId.Name))
            {
                otlpTagWriterState = AddLogAttribute(ref otlpTagWriterState, ExperimentalOptions.LogRecordEventNameAttribute, logRecord.EventId.Name!, attributeCountLimit, attributeValueLengthLimit);
            }
        }

        if (logRecord.Exception != null)
        {
            otlpTagWriterState = AddLogAttribute(ref otlpTagWriterState, SemanticConventions.AttributeExceptionType, logRecord.Exception.GetType().Name, attributeCountLimit, attributeValueLengthLimit);
            otlpTagWriterState = AddLogAttribute(ref otlpTagWriterState, SemanticConventions.AttributeExceptionMessage, logRecord.Exception.Message, attributeCountLimit, attributeValueLengthLimit);
            otlpTagWriterState = AddLogAttribute(ref otlpTagWriterState, SemanticConventions.AttributeExceptionStacktrace, logRecord.Exception.ToInvariantString(), attributeCountLimit, attributeValueLengthLimit);
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
                if (attribute.Key.Equals("{OriginalFormat}") && !bodyPopulatedFromFormattedMessage)
                {
                    otlpTagWriterState.WritePosition = WriteLogRecordBody(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, (attribute.Value as string).AsSpan());
                    isLogRecordBodySet = true;
                }
                else
                {
                    otlpTagWriterState = AddLogAttribute(ref otlpTagWriterState, attribute, attributeCountLimit, attributeValueLengthLimit);
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

        /*
         * TODO: Handle scopes, otlpTagWriterState needs to be passed as ref.
        logRecord.ForEachScope(ProcessScope, otlpTagWriterState);

        void ProcessScope(LogRecordScope scope, ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState)
        {
            foreach (var scopeItem in scope)
            {
                if (scopeItem.Key.Equals("{OriginalFormat}") || string.IsNullOrEmpty(scopeItem.Key))
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
                    otlpTagWriterState = AddLogAttribute(ref otlpTagWriterState, scopeItem, attributeCountLimit, attributeValueLengthLimit);
                }
            }
        }
        */

        if (otlpTagWriterState.DroppedTagCount > 0)
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Dropped_Attributes_Count, ProtobufWireType.VARINT);
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteVarInt32(buffer, otlpTagWriterState.WritePosition, (uint)otlpTagWriterState.DroppedTagCount);
        }

        ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer, logRecordLengthPosition, otlpTagWriterState.WritePosition - (logRecordLengthPosition + ReserveSizeForLength));

        return otlpTagWriterState.WritePosition;
    }

    private static int WriteLogRecordBody(byte[] buffer, int writePosition, ReadOnlySpan<char> value)
    {
        var numberOfUtf8CharsInString = ProtobufSerializer.GetNumberOfUtf8CharsInString(value);
        var serializedLengthSize = ProtobufSerializer.ComputeVarInt64Size((ulong)numberOfUtf8CharsInString);

        // length = numberOfUtf8CharsInString + tagSize + length field size.
        writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, numberOfUtf8CharsInString + 1 + serializedLengthSize, ProtobufOtlpLogFieldNumberConstants.LogRecord_Body, ProtobufWireType.LEN);
        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpTraceFieldNumberConstants.AnyValue_String_Value, numberOfUtf8CharsInString, value);
        return writePosition;
    }

    private static ProtobufOtlpTagWriter.OtlpTagWriterState AddLogAttribute(ref ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState, KeyValuePair<string, object?> attribute, int maxAttributeCount, int? maxValueLength)
    {
        return AddLogAttribute(ref otlpTagWriterState, attribute.Key, attribute.Value, maxAttributeCount, maxValueLength);
    }

    private static ProtobufOtlpTagWriter.OtlpTagWriterState AddLogAttribute(ref ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState, string key, object? value, int maxAttributeCount, int? maxValueLength)
    {
        if (otlpTagWriterState.TagCount == maxAttributeCount)
        {
            otlpTagWriterState.DroppedTagCount++;
        }
        else
        {
            otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(otlpTagWriterState.Buffer, otlpTagWriterState.WritePosition, ProtobufOtlpLogFieldNumberConstants.LogRecord_Attributes, ProtobufWireType.LEN);
            int logAttributesLengthPosition = otlpTagWriterState.WritePosition;
            otlpTagWriterState.WritePosition += ReserveSizeForLength;

            ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, key, value, maxValueLength);

            var logAttributesLength = otlpTagWriterState.WritePosition - (logAttributesLengthPosition + ReserveSizeForLength);
            ProtobufSerializer.WriteReservedLength(otlpTagWriterState.Buffer, logAttributesLengthPosition, logAttributesLength);
            otlpTagWriterState.TagCount++;
        }

        return otlpTagWriterState;
    }
}
