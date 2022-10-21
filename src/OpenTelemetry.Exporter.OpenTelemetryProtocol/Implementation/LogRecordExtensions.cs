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
using OpenTelemetry.Internal;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpLogs = OpenTelemetry.Proto.Logs.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    internal static class LogRecordExtensions
    {
        private static readonly string[] SeverityTextMapping = new string[]
        {
            "Trace", "Debug", "Information", "Warning", "Error", "Fatal",
        };

        internal static void AddBatch(
            this OtlpCollector.ExportLogsServiceRequest request,
            OtlpResource.Resource processResource,
            in Batch<LogRecord> logRecordBatch)
        {
            Dictionary<string, OtlpLogs.ScopeLogs> logsByLibrary = new Dictionary<string, OtlpLogs.ScopeLogs>();
            var resourceLogs = new OtlpLogs.ResourceLogs
            {
                Resource = processResource,
            };
            request.ResourceLogs.Add(resourceLogs);

            OtlpLogs.ScopeLogs currentScopeLogs = null;

            foreach (var logRecord in logRecordBatch)
            {
                var otlpLogRecord = logRecord.ToOtlpLog();
                if (otlpLogRecord != null)
                {
                    var instrumentationScope = logRecord.InstrumentationScope;

                    var instrumentationScopeName = instrumentationScope.Name ?? string.Empty;

                    if (currentScopeLogs == null || currentScopeLogs.Scope.Name != instrumentationScopeName)
                    {
                        if (!logsByLibrary.TryGetValue(instrumentationScopeName, out var scopeLogs))
                        {
                            var scope = new OtlpCommon.InstrumentationScope()
                            {
                                Name = instrumentationScopeName,
                            };

                            if (instrumentationScope?.Version != null)
                            {
                                scope.Version = instrumentationScope.Version;
                            }

                            var attributes = instrumentationScope?.Attributes;
                            if (attributes != null)
                            {
                                foreach (var attribute in attributes)
                                {
                                    if (OtlpKeyValueTransformer.Instance.TryTransformTag(
                                        attribute,
                                        out var otlpAttribute))
                                    {
                                        scope.Attributes.Add(otlpAttribute);
                                    }
                                }
                            }

                            scopeLogs = new OtlpLogs.ScopeLogs
                            {
                                Scope = scope,
                            };

                            logsByLibrary.Add(instrumentationScopeName, scopeLogs);
                            resourceLogs.ScopeLogs.Add(scopeLogs);
                        }

                        currentScopeLogs = scopeLogs;
                    }

                    currentScopeLogs.LogRecords.Add(otlpLogRecord);
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
                    SeverityNumber = GetSeverityNumber(logRecord.Severity),
                };

                if (!string.IsNullOrWhiteSpace(logRecord.SeverityText))
                {
                    otlpLogRecord.SeverityText = logRecord.SeverityText;
                }
                else if (logRecord.Severity.HasValue)
                {
                    uint severityNumber = (uint)logRecord.Severity.Value;
                    if (severityNumber < 6)
                    {
                        otlpLogRecord.SeverityText = SeverityTextMapping[severityNumber];
                    }
                }

                if (!string.IsNullOrEmpty(logRecord.CategoryName))
                {
                    // TODO:
                    // 1. Track the following issue, and map CategoryName to Name
                    // if it makes it to log data model.
                    // https://github.com/open-telemetry/opentelemetry-specification/issues/2398
                    // 2. Confirm if this name for attribute is good.
                    otlpLogRecord.Attributes.AddStringAttribute("dotnet.ilogger.category", logRecord.CategoryName);
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
                        else if (OtlpKeyValueTransformer.Instance.TryTransformTag(attribute, out var result))
                        {
                            otlpLogRecord.Attributes.Add(result);
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
                        if (OtlpKeyValueTransformer.Instance.TryTransformTag(scopeItemWithDepthInfo, out var result))
                        {
                            otlpLog.Attributes.Add(result);
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
        private static OtlpLogs.SeverityNumber GetSeverityNumber(LogRecordSeverity? severity)
        {
            if (!severity.HasValue)
            {
                return OtlpLogs.SeverityNumber.Unspecified;
            }

            // TODO: for improving perf simply do ((int)loglevel * 4) + 1
            // or ((int)logLevel << 2) + 1
            // Current code is just for ease of reading.
            switch (severity.Value)
            {
                case LogRecordSeverity.Trace:
                    return OtlpLogs.SeverityNumber.Trace;
                case LogRecordSeverity.Debug:
                    return OtlpLogs.SeverityNumber.Debug;
                case LogRecordSeverity.Information:
                    return OtlpLogs.SeverityNumber.Info;
                case LogRecordSeverity.Warning:
                    return OtlpLogs.SeverityNumber.Warn;
                case LogRecordSeverity.Error:
                    return OtlpLogs.SeverityNumber.Error;
                case LogRecordSeverity.Fatal:
                    return OtlpLogs.SeverityNumber.Fatal;

                // TODO:
                // we reach default only for invalid/unknown values should we
                // throw here then?
                default:
                    return OtlpLogs.SeverityNumber.Trace;
            }
        }
    }
}
