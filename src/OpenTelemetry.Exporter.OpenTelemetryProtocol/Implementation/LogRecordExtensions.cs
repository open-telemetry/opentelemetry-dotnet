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

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using OtlpCollector = Opentelemetry.Proto.Collector.Logs.V1;
using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpLogs = Opentelemetry.Proto.Logs.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    internal static class LogRecordExtensions
    {
        private static readonly string[] LogLevels = new string[7]
        {
            "Trace", "Debug", "Information", "Warning", "Error", "Critical", "None",
        };

        internal static void AddBatch(
            this OtlpCollector.ExportLogsServiceRequest request,
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
                var otlpLogRecord = logRecord.ToOtlpLog();
                if (otlpLogRecord != null)
                {
                    scopeLogs.LogRecords.Add(otlpLogRecord);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static OtlpLogs.LogRecord ToOtlpLog(this LogRecord logRecord)
        {
            OtlpLogs.LogRecord otlpLogRecord = null;

            try
            {
                otlpLogRecord = new OtlpLogs.LogRecord
                {
                    TimeUnixNano = (ulong)logRecord.Timestamp.ToUnixTimeNanoseconds(),
                    SeverityNumber = GetSeverityNumber(logRecord.LogLevel),
                    SeverityText = LogLevels[(int)logRecord.LogLevel],
                };

                // TODO: Add logRecord.CategoryName as an attribute

                bool bodyPopulatedFromFormattedMessage = false;
                if (logRecord.FormattedMessage != null)
                {
                    otlpLogRecord.Body = new OtlpCommon.AnyValue { StringValue = logRecord.FormattedMessage };
                    bodyPopulatedFromFormattedMessage = true;
                }

                if (logRecord.StateValues != null)
                {
                    foreach (var stateValue in logRecord.StateValues)
                    {
                        // Special casing {OriginalFormat}
                        // See https://github.com/open-telemetry/opentelemetry-dotnet/pull/3182
                        // for explanation.
                        if (stateValue.Key.Equals("{OriginalFormat}") && !bodyPopulatedFromFormattedMessage)
                        {
                            otlpLogRecord.Body = new OtlpCommon.AnyValue { StringValue = stateValue.Value as string };
                        }
                        else
                        {
                            var otlpAttribute = stateValue.ToOtlpAttribute();
                            otlpLogRecord.Attributes.Add(otlpAttribute);
                        }
                    }
                }

                if (logRecord.EventId.Id != default)
                {
                    otlpLogRecord.Attributes.AddIntAttribute(nameof(logRecord.EventId.Id), logRecord.EventId.Id);
                }

                if (!string.IsNullOrEmpty(logRecord.EventId.Name))
                {
                    otlpLogRecord.Attributes.AddStringAttribute(nameof(logRecord.EventId.Name), logRecord.EventId.Name);
                }

                if (logRecord.Exception != null)
                {
                    otlpLogRecord.Attributes.AddStringAttribute(SemanticConventions.AttributeExceptionType, logRecord.Exception.GetType().Name);
                    otlpLogRecord.Attributes.AddStringAttribute(SemanticConventions.AttributeExceptionMessage, logRecord.Exception.Message);
                    otlpLogRecord.Attributes.AddStringAttribute(SemanticConventions.AttributeExceptionStacktrace, logRecord.Exception.ToInvariantString());
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

                int scopeDepth = -1;
                logRecord.ForEachScope(ProcessScope, otlpLogRecord);

                void ProcessScope(LogRecordScope scope, OtlpLogs.LogRecord otlpLog)
                {
                    scopeDepth++;
                    foreach (var scopeItem in scope)
                    {
                        var scopeItemWithDepthInfo = new KeyValuePair<string, object>($"[Scope.{scopeDepth}]:{scopeItem.Key}", scopeItem.Value);
                        var otlpAttribute = scopeItemWithDepthInfo.ToOtlpAttribute();
                        otlpLog.Attributes.Add(otlpAttribute);
                    }
                }
            }
            catch (Exception ex)
            {
                OpenTelemetryProtocolExporterEventSource.Log.CouldNotTranslateLogRecord(ex.Message);
            }

            return otlpLogRecord;
        }

        private static void AddStringAttribute(this RepeatedField<OtlpCommon.KeyValue> repeatedField, string key, string value)
        {
            repeatedField.Add(new OtlpCommon.KeyValue
            {
                Key = key,
                Value = new OtlpCommon.AnyValue { StringValue = value },
            });
        }

        private static void AddIntAttribute(this RepeatedField<OtlpCommon.KeyValue> repeatedField, string key, int value)
        {
            repeatedField.Add(new OtlpCommon.KeyValue
            {
                Key = key,
                Value = new OtlpCommon.AnyValue { IntValue = value },
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OtlpLogs.SeverityNumber GetSeverityNumber(LogLevel logLevel)
        {
            // Maps the ILogger LogLevel to OpenTelemetry logging level.
            // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/logs/data-model.md#appendix-b-severitynumber-example-mappings
            // TODO: for improving perf simply do ((int)loglevel * 4) + 1
            // or ((int)logLevel << 2) + 1
            // Current code is just for ease of reading.
            switch (logLevel)
            {
                case LogLevel.Trace:
                    return OtlpLogs.SeverityNumber.Trace;
                case LogLevel.Debug:
                    return OtlpLogs.SeverityNumber.Debug;
                case LogLevel.Information:
                    return OtlpLogs.SeverityNumber.Info;
                case LogLevel.Warning:
                    return OtlpLogs.SeverityNumber.Warn;
                case LogLevel.Error:
                    return OtlpLogs.SeverityNumber.Error;
                case LogLevel.Critical:
                    return OtlpLogs.SeverityNumber.Fatal;

                // TODO:
                // we reach default only for LogLevel.None
                // but that is filtered out anyway.
                // should we throw here then?
                default:
                    return OtlpLogs.SeverityNumber.Debug;
            }
        }
    }
}
