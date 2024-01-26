// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpLogs = OpenTelemetry.Proto.Logs.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal sealed class OtlpLogRecordTransformer
{
    internal static readonly ConcurrentBag<OtlpLogs.ScopeLogs> LogListPool = new();

    private readonly SdkLimitOptions sdkLimitOptions;
    private readonly ExperimentalOptions experimentalOptions;

    public OtlpLogRecordTransformer(SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions)
    {
        this.sdkLimitOptions = sdkLimitOptions;
        this.experimentalOptions = experimentalOptions;
    }

    internal OtlpCollector.ExportLogsServiceRequest BuildExportRequest(
        OtlpResource.Resource processResource,
        in Batch<LogRecord> logRecordBatch)
    {
        // TODO: https://github.com/open-telemetry/opentelemetry-dotnet/issues/4943
        Dictionary<string, OtlpLogs.ScopeLogs> logsByCategory = new Dictionary<string, OtlpLogs.ScopeLogs>();

        var request = new OtlpCollector.ExportLogsServiceRequest();

        var resourceLogs = new OtlpLogs.ResourceLogs
        {
            Resource = processResource,
        };
        request.ResourceLogs.Add(resourceLogs);

        foreach (var logRecord in logRecordBatch)
        {
            var otlpLogRecord = this.ToOtlpLog(logRecord);
            if (otlpLogRecord != null)
            {
                if (!logsByCategory.TryGetValue(logRecord.CategoryName, out var scopeLogs))
                {
                    scopeLogs = this.GetLogListFromPool(logRecord.CategoryName);
                    logsByCategory.Add(logRecord.CategoryName, scopeLogs);
                    resourceLogs.ScopeLogs.Add(scopeLogs);
                }

                scopeLogs.LogRecords.Add(otlpLogRecord);
            }
        }

        return request;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Return(OtlpCollector.ExportLogsServiceRequest request)
    {
        var resourceLogs = request.ResourceLogs.FirstOrDefault();
        if (resourceLogs == null)
        {
            return;
        }

        foreach (var scope in resourceLogs.ScopeLogs)
        {
            scope.LogRecords.Clear();
            LogListPool.Add(scope);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal OtlpLogs.ScopeLogs GetLogListFromPool(string name)
    {
        if (!LogListPool.TryTake(out var logs))
        {
            logs = new OtlpLogs.ScopeLogs
            {
                Scope = new OtlpCommon.InstrumentationScope
                {
                    Name = name, // Name is enforced to not be null, but it can be empty.
                    Version = string.Empty, // proto requires this to be non-null.
                },
            };
        }
        else
        {
            logs.Scope.Name = name;
            logs.Scope.Version = string.Empty;
        }

        return logs;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal OtlpLogs.LogRecord ToOtlpLog(LogRecord logRecord)
    {
        OtlpLogs.LogRecord otlpLogRecord = null;

        try
        {
            var timestamp = (ulong)logRecord.Timestamp.ToUnixTimeNanoseconds();
            otlpLogRecord = new OtlpLogs.LogRecord
            {
                TimeUnixNano = timestamp,
                ObservedTimeUnixNano = timestamp,
                SeverityNumber = GetSeverityNumber(logRecord.Severity),
            };

            if (!string.IsNullOrWhiteSpace(logRecord.SeverityText))
            {
                otlpLogRecord.SeverityText = logRecord.SeverityText;
            }
            else if (logRecord.Severity.HasValue)
            {
                otlpLogRecord.SeverityText = logRecord.Severity.Value.ToShortName();
            }

            var attributeValueLengthLimit = this.sdkLimitOptions.LogRecordAttributeValueLengthLimit;
            var attributeCountLimit = this.sdkLimitOptions.LogRecordAttributeCountLimit ?? int.MaxValue;

            if (this.experimentalOptions.EmitLogEventAttributes)
            {
                if (logRecord.EventId.Id != default)
                {
                    AddIntAttribute(otlpLogRecord, ExperimentalOptions.LogRecordEventIdAttribute, logRecord.EventId.Id, attributeCountLimit);
                }

                if (!string.IsNullOrEmpty(logRecord.EventId.Name))
                {
                    AddStringAttribute(otlpLogRecord, ExperimentalOptions.LogRecordEventNameAttribute, logRecord.EventId.Name, attributeValueLengthLimit, attributeCountLimit);
                }
            }

            if (logRecord.Exception != null)
            {
                AddStringAttribute(otlpLogRecord, SemanticConventions.AttributeExceptionType, logRecord.Exception.GetType().Name, attributeValueLengthLimit, attributeCountLimit);
                AddStringAttribute(otlpLogRecord, SemanticConventions.AttributeExceptionMessage, logRecord.Exception.Message, attributeValueLengthLimit, attributeCountLimit);
                AddStringAttribute(otlpLogRecord, SemanticConventions.AttributeExceptionStacktrace, logRecord.Exception.ToInvariantString(), attributeValueLengthLimit, attributeCountLimit);
            }

            bool bodyPopulatedFromFormattedMessage = false;
            if (logRecord.FormattedMessage != null)
            {
                otlpLogRecord.Body = new OtlpCommon.AnyValue { StringValue = logRecord.FormattedMessage };
                bodyPopulatedFromFormattedMessage = true;
            }

            if (logRecord.Attributes != null)
            {
                bool useTemplateFromExtensionMethod = false;
                foreach (var attribute in logRecord.Attributes)
                {
                    // Special casing {OriginalFormat}
                    // See https://github.com/open-telemetry/opentelemetry-dotnet/pull/3182
                    // for explanation.
                    if (attribute.Key.Equals("{OriginalFormat}") && !bodyPopulatedFromFormattedMessage)
                    {
                        otlpLogRecord.Body = new OtlpCommon.AnyValue { StringValue = attribute.Value as string };
                        useTemplateFromExtensionMethod = true;
                    }
                    else if (OtlpKeyValueTransformer.Instance.TryTransformTag(attribute, out var result, attributeValueLengthLimit))
                    {
                        AddAttribute(otlpLogRecord, result, attributeCountLimit);
                    }
                }

                // Supports Body set directly on LogRecord for the Logs Bridge API.
                if (!useTemplateFromExtensionMethod && !bodyPopulatedFromFormattedMessage && logRecord.Body != null)
                {
                    // If {OriginalFormat} is not present in the attributes,
                    // use logRecord.Body if it is set.
                    otlpLogRecord.Body = new OtlpCommon.AnyValue { StringValue = logRecord.Body };
                }
            }

            if (logRecord.TraceId != default && logRecord.SpanId != default)
            {
                byte[] traceIdBytes = new byte[16];
                byte[] spanIdBytes = new byte[8];

                logRecord.TraceId.CopyTo(traceIdBytes);
                logRecord.SpanId.CopyTo(spanIdBytes);

                otlpLogRecord.TraceId = UnsafeByteOperations.UnsafeWrap(traceIdBytes);
                otlpLogRecord.SpanId = UnsafeByteOperations.UnsafeWrap(spanIdBytes);
                otlpLogRecord.Flags = (uint)logRecord.TraceFlags;
            }

            logRecord.ForEachScope(ProcessScope, otlpLogRecord);

            void ProcessScope(LogRecordScope scope, OtlpLogs.LogRecord otlpLog)
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
                        if (OtlpKeyValueTransformer.Instance.TryTransformTag(scopeItem, out var result, attributeValueLengthLimit))
                        {
                            AddAttribute(otlpLog, result, attributeCountLimit);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            OpenTelemetryProtocolExporterEventSource.Log.CouldNotTranslateLogRecord(ex.Message);
        }

        return otlpLogRecord;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddAttribute(OtlpLogs.LogRecord logRecord, OtlpCommon.KeyValue attribute, int maxAttributeCount)
    {
        if (logRecord.Attributes.Count < maxAttributeCount)
        {
            logRecord.Attributes.Add(attribute);
        }
        else
        {
            logRecord.DroppedAttributesCount++;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddStringAttribute(OtlpLogs.LogRecord logRecord, string key, string value, int? maxValueLength, int maxAttributeCount)
    {
        var attributeItem = new KeyValuePair<string, object>(key, value);
        if (OtlpKeyValueTransformer.Instance.TryTransformTag(attributeItem, out var result, maxValueLength))
        {
            AddAttribute(logRecord, result, maxAttributeCount);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddIntAttribute(OtlpLogs.LogRecord logRecord, string key, int value, int maxAttributeCount)
    {
        var attributeItem = new KeyValuePair<string, object>(key, value);
        if (OtlpKeyValueTransformer.Instance.TryTransformTag(attributeItem, out var result))
        {
            AddAttribute(logRecord, result, maxAttributeCount);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OtlpLogs.SeverityNumber GetSeverityNumber(LogRecordSeverity? severity)
    {
        if (!severity.HasValue)
        {
            return OtlpLogs.SeverityNumber.Unspecified;
        }

        return (OtlpLogs.SeverityNumber)(int)severity.Value;
    }
}
