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
using OpenTelemetry.Logs;
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
            byte[] traceIdBytes = new byte[16];
            byte[] spanIdBytes = new byte[8];

            logRecord.TraceId.CopyTo(traceIdBytes);
            logRecord.SpanId.CopyTo(spanIdBytes);

            var otlpLogRecord = new OtlpLogs.LogRecord
            {
                TimeUnixNano = (ulong)logRecord.Timestamp.ToUnixTimeNanoseconds(),
                Name = logRecord.CategoryName,
                Body = new OtlpCommon.AnyValue { StringValue = logRecord.State.ToString() },
                SeverityText = logRecord.LogLevel.ToString(),
                TraceId = UnsafeByteOperations.UnsafeWrap(traceIdBytes),
                SpanId = UnsafeByteOperations.UnsafeWrap(spanIdBytes),

                // TODO: Handle remaining fields
                // Flags
                // SeverityNumber
                // Attributes
                // DroppedAttributesCount
            };

            return otlpLogRecord;
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

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static OtlpCommon.KeyValue CreateOtlpKeyValue(string key, OtlpCommon.AnyValue value)
        {
            return new OtlpCommon.KeyValue { Key = key, Value = value };
        }
    }
}
#endif
