// <copyright file="LogRecordExtensions.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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

internal static class LogRecordExtensions
{
    internal static void AddBatch(
        this OtlpCollector.ExportLogsServiceRequest request,
        SdkLimitOptions sdkLimitOptions,
        ExperimentalOptions experimentalOptions,
        OtlpResource.Resource processResource,
        in Batch<LogRecord> logRecordBatch)
    {
        var resourceLogs = new OtlpLogs.ResourceLogs
        {
            Resource = processResource,
        };
        request.ResourceLogs.Add(resourceLogs);

        var scopeLogs = new OtlpLogs.ScopeLogs();
        resourceLogs.ScopeLogs.Add(scopeLogs);

        foreach (var logRecord in logRecordBatch)
        {
            var otlpLogRecord = logRecord.ToOtlpLog(sdkLimitOptions, experimentalOptions);
            if (otlpLogRecord != null)
            {
                scopeLogs.LogRecords.Add(otlpLogRecord);
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OtlpLogs.LogRecord ToOtlpLog(this LogRecord logRecord, SdkLimitOptions sdkLimitOptions, ExperimentalOptions experimentalOptions)
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

            var attributeValueLengthLimit = sdkLimitOptions.AttributeValueLengthLimit;
            var attributeCountLimit = sdkLimitOptions.AttributeCountLimit ?? int.MaxValue;

            /*
            // Removing this temporarily for stable release
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/4776
            // https://github.com/open-telemetry/opentelemetry-dotnet/issues/3491
            // First add the generic attributes like Category, EventId and Exception,
            // so they are less likely being dropped because of AttributeCountLimit.

            if (!string.IsNullOrEmpty(logRecord.CategoryName))
            {
                // TODO:
                // 1. Track the following issue, and map CategoryName to Name
                // if it makes it to log data model.
                // https://github.com/open-telemetry/opentelemetry-specification/issues/2398
                // 2. Confirm if this name for attribute is good.
                otlpLogRecord.AddStringAttribute("dotnet.ilogger.category", logRecord.CategoryName, attributeValueLengthLimit, attributeCountLimit);
            }

            if (logRecord.EventId.Id != default)
            {
                otlpLogRecord.AddIntAttribute(nameof(logRecord.EventId.Id), logRecord.EventId.Id, attributeCountLimit);
            }

            if (!string.IsNullOrEmpty(logRecord.EventId.Name))
            {
                otlpLogRecord.AddStringAttribute(nameof(logRecord.EventId.Name), logRecord.EventId.Name, attributeValueLengthLimit, attributeCountLimit);
            }
            */

            if (experimentalOptions.EmitLogExceptionAttributes && logRecord.Exception != null)
            {
                otlpLogRecord.AddStringAttribute(SemanticConventions.AttributeExceptionType, logRecord.Exception.GetType().Name, attributeValueLengthLimit, attributeCountLimit);
                otlpLogRecord.AddStringAttribute(SemanticConventions.AttributeExceptionMessage, logRecord.Exception.Message, attributeValueLengthLimit, attributeCountLimit);
                otlpLogRecord.AddStringAttribute(SemanticConventions.AttributeExceptionStacktrace, logRecord.Exception.ToInvariantString(), attributeValueLengthLimit, attributeCountLimit);
            }

            bool bodyPopulatedFromFormattedMessage = false;
            if (logRecord.FormattedMessage != null)
            {
                otlpLogRecord.Body = new OtlpCommon.AnyValue { StringValue = logRecord.FormattedMessage };
                bodyPopulatedFromFormattedMessage = true;
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
                        otlpLogRecord.Body = new OtlpCommon.AnyValue { StringValue = attribute.Value as string };
                    }
                    else if (OtlpKeyValueTransformer.Instance.TryTransformTag(attribute, out var result, attributeValueLengthLimit))
                    {
                        otlpLogRecord.AddAttribute(result, attributeCountLimit);
                    }
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
                            otlpLog.AddAttribute(result, attributeCountLimit);
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
    private static void AddAttribute(this OtlpLogs.LogRecord logRecord, OtlpCommon.KeyValue attribute, int maxAttributeCount)
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
    private static void AddStringAttribute(this OtlpLogs.LogRecord logRecord, string key, string value, int? maxValueLength, int maxAttributeCount)
    {
        var attributeItem = new KeyValuePair<string, object>(key, value);
        if (OtlpKeyValueTransformer.Instance.TryTransformTag(attributeItem, out var result, maxValueLength))
        {
            logRecord.AddAttribute(result, maxAttributeCount);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddIntAttribute(this OtlpLogs.LogRecord logRecord, string key, int value, int maxAttributeCount)
    {
        var attributeItem = new KeyValuePair<string, object>(key, value);
        if (OtlpKeyValueTransformer.Instance.TryTransformTag(attributeItem, out var result))
        {
            logRecord.AddAttribute(result, maxAttributeCount);
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
