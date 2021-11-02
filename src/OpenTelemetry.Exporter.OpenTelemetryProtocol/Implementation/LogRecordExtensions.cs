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
using Google.Protobuf.Collections;
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
        internal static void AddBatch(
            this OtlpCollector.ExportLogsServiceRequest request,
            OtlpResource.Resource processResource,
            in Batch<LogRecord> logRecordBatch)
        {
            OtlpLogs.ResourceLogs resourceLogs = new OtlpLogs.ResourceLogs
            {
                Resource = processResource,
            };
            request.ResourceLogs.Add(resourceLogs);

            var instrumentationLibraryLogs = new OtlpLogs.InstrumentationLibraryLogs();
            resourceLogs.InstrumentationLibraryLogs.Add(instrumentationLibraryLogs);

            foreach (var item in logRecordBatch)
            {
                var logRecord = item.ToOtlpLog();
                if (logRecord == null)
                {
                    OpenTelemetryProtocolExporterEventSource.Log.CouldNotTranslateLogRecord(
                        nameof(LogRecordExtensions),
                        nameof(AddBatch));
                    continue;
                }

                instrumentationLibraryLogs.Logs.Add(logRecord);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static OtlpLogs.LogRecord ToOtlpLog(this LogRecord logRecord)
        {
            var otlpLogRecord = new OtlpLogs.LogRecord
            {
                TimeUnixNano = (ulong)logRecord.Timestamp.ToUnixTimeNanoseconds(),
                Name = logRecord.CategoryName,

                // TODO: Devise mapping of LogLevel to SeverityNumber
                // See: https://github.com/open-telemetry/opentelemetry-proto/blob/bacfe08d84e21fb2a779e302d12e8dfeb67e7b86/opentelemetry/proto/logs/v1/logs.proto#L100-L102
                SeverityText = logRecord.LogLevel.ToString(),
            };

            if (logRecord.FormattedMessage != null)
            {
                otlpLogRecord.Body = new OtlpCommon.AnyValue { StringValue = logRecord.FormattedMessage };
            }

            if (logRecord.EventId != 0)
            {
                otlpLogRecord.Attributes.AddAttribute(nameof(logRecord.EventId), logRecord.EventId.ToString());
            }

            if (logRecord.Exception != null)
            {
                otlpLogRecord.Attributes.AddAttribute(SemanticConventions.AttributeExceptionType, logRecord.Exception.GetType().Name);
                otlpLogRecord.Attributes.AddAttribute(SemanticConventions.AttributeExceptionMessage, logRecord.Exception.Message);
                otlpLogRecord.Attributes.AddAttribute(SemanticConventions.AttributeExceptionStacktrace, logRecord.Exception.ToInvariantString());
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

            // TODO: Add additional attributes from scope and state
            // Might make sense to take an approach similar to https://github.com/open-telemetry/opentelemetry-dotnet-contrib/blob/897b734aa5ea9992538f04f6ea6871fe211fa903/src/OpenTelemetry.Contrib.Preview/Internal/DefaultLogStateConverter.cs

            return otlpLogRecord;
        }

        private static void AddAttribute(this RepeatedField<OtlpCommon.KeyValue> repeatedField, string key, string value)
        {
            repeatedField.Add(new OtlpCommon.KeyValue
            {
                Key = key,
                Value = new OtlpCommon.AnyValue { StringValue = value },
            });
        }
    }
}
