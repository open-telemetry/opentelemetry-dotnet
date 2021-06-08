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

#if NET461 || NETSTANDARD2_0 || NETSTANDARD2_1
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
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
        private static readonly ConcurrentBag<OtlpLogs.InstrumentationLibraryLogs> LogListPool = new ConcurrentBag<OtlpLogs.InstrumentationLibraryLogs>();
        private static readonly Action<RepeatedField<OtlpLogs.LogRecord>, int> RepeatedFieldOfLogSetCountAction = CreateRepeatedFieldOfLogSetCountAction();

        internal static void AddBatch(
            this OtlpCollector.ExportLogsServiceRequest request,
            OtlpResource.Resource processResource,
            in Batch<LogRecord> logRecordBatch)
        {
            Dictionary<string, OtlpLogs.InstrumentationLibraryLogs> logRecordsByLibrary = new Dictionary<string, OtlpLogs.InstrumentationLibraryLogs>();
            OtlpLogs.ResourceLogs resourceLogs = new OtlpLogs.ResourceLogs
            {
                Resource = processResource,
            };
            request.ResourceLogs.Add(resourceLogs);

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

                // TODO: Record source/version correctly
                var logRecordSourceName = "OpenTelemetry Logs";
                if (!logRecordsByLibrary.TryGetValue(logRecordSourceName, out var logRecords))
                {
                    logRecords = GetLogListFromPool(logRecordSourceName, "1.2.3");

                    logRecordsByLibrary.Add(logRecordSourceName, logRecords);
                    resourceLogs.InstrumentationLibraryLogs.Add(logRecords);
                }

                logRecords.Logs.Add(logRecord);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void Return(this OtlpCollector.ExportLogsServiceRequest request)
        {
            var resourceLogs = request.ResourceLogs.FirstOrDefault();
            if (resourceLogs == null)
            {
                return;
            }

            foreach (var libraryLogs in resourceLogs.InstrumentationLibraryLogs)
            {
                RepeatedFieldOfLogSetCountAction(libraryLogs.Logs, 0);
                LogListPool.Add(libraryLogs);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static OtlpLogs.InstrumentationLibraryLogs GetLogListFromPool(string name, string version)
        {
            if (!LogListPool.TryTake(out var logs))
            {
                logs = new OtlpLogs.InstrumentationLibraryLogs
                {
                    InstrumentationLibrary = new OtlpCommon.InstrumentationLibrary
                    {
                        Name = name, // Name is enforced to not be null, but it can be empty.
                        Version = version ?? string.Empty, // NRE throw by proto
                    },
                };
            }
            else
            {
                logs.InstrumentationLibrary.Name = name;
                logs.InstrumentationLibrary.Version = version ?? string.Empty;
            }

            return logs;
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

        private static Action<RepeatedField<OtlpLogs.LogRecord>, int> CreateRepeatedFieldOfLogSetCountAction()
        {
            FieldInfo repeatedFieldOfLogCountField = typeof(RepeatedField<OtlpLogs.LogRecord>).GetField("count", BindingFlags.NonPublic | BindingFlags.Instance);

            DynamicMethod dynamicMethod = new DynamicMethod(
                "CreateSetCountAction",
                null,
                new[] { typeof(RepeatedField<OtlpLogs.LogRecord>), typeof(int) },
                typeof(LogRecordExtensions).Module,
                skipVisibility: true);

            var generator = dynamicMethod.GetILGenerator();

            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stfld, repeatedFieldOfLogCountField);
            generator.Emit(OpCodes.Ret);

            return (Action<RepeatedField<OtlpLogs.LogRecord>, int>)dynamicMethod.CreateDelegate(typeof(Action<RepeatedField<OtlpLogs.LogRecord>, int>));
        }
    }
}
#endif
