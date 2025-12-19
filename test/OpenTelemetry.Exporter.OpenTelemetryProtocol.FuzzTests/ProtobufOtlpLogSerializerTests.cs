// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Logs;
using OtlpCollector = OpenTelemetry.Proto.Collector.Logs.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.FuzzTests;

public class ProtobufOtlpLogSerializerTests
{
    [Property(MaxTest = 100)]
    public Property SerializedDataNeverExceedsBufferSize() => Prop.ForAll(
        Generators.BufferSizeArbitrary(),
        Generators.SdkLimitOptionsArbitrary(),
        Generators.LogRecordSeverityArbitrary(),
        (bufferSize, sdkLimits, severity) =>
        {
            var logRecords = CreateLogRecords(severity);

            try
            {
                var buffer = new byte[bufferSize];
                var batch = new Batch<LogRecord>(logRecords, logRecords.Length);
                var experimentalOptions = new ExperimentalOptions();

                var writePos = ProtobufOtlpLogSerializer.WriteLogsData(
                    ref buffer,
                    0,
                    sdkLimits,
                    experimentalOptions,
                    null,
                    batch);

                return writePos >= 0 && writePos <= buffer.Length;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 100)]
    public Property WriteLogsDataReturnsNonNegativePosition() => Prop.ForAll(
        Generators.SdkLimitOptionsArbitrary(),
        Generators.ResourceArbitrary(),
        Generators.LogRecordSeverityArbitrary(),
        (sdkLimits, resource, severity) =>
        {
            var logRecords = CreateLogRecords(severity);

            try
            {
                var buffer = new byte[10 * 1024 * 1024];
                var batch = new Batch<LogRecord>(logRecords, logRecords.Length);
                var experimentalOptions = new ExperimentalOptions();

                var writePos = ProtobufOtlpLogSerializer.WriteLogsData(
                    ref buffer,
                    0,
                    sdkLimits,
                    experimentalOptions,
                    resource,
                    batch);

                return writePos >= 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 50)]
    public Property WriteLogsDataHandlesEmptyBatches() => Prop.ForAll(
        Generators.SdkLimitOptionsArbitrary(),
        sdkLimits =>
        {
            try
            {
                var buffer = new byte[1024];
                var batch = new Batch<LogRecord>([], 0);
                var experimentalOptions = new ExperimentalOptions();

                var writePos = ProtobufOtlpLogSerializer.WriteLogsData(
                    ref buffer,
                    0,
                    sdkLimits,
                    experimentalOptions,
                    null,
                    batch);

                return writePos >= 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 50)]
    public Property BufferAutoResizesWhenNeeded() => Prop.ForAll(
        Generators.SdkLimitOptionsArbitrary(),
        Generators.ResourceArbitrary(),
        Generators.LogRecordSeverityArbitrary(),
        (sdkLimits, resource, severity) =>
        {
            var logRecords = CreateLogRecords(severity);

            try
            {
                var buffer = new byte[64]; // Intentionally small
                var initialLength = buffer.Length;
                var batch = new Batch<LogRecord>(logRecords, logRecords.Length);
                var experimentalOptions = new ExperimentalOptions();

                var writePos = ProtobufOtlpLogSerializer.WriteLogsData(
                    ref buffer,
                    0,
                    sdkLimits,
                    experimentalOptions,
                    resource,
                    batch);

                return buffer.Length >= initialLength && writePos >= 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 50)]
    public Property SerializedOutputCanBeDeserialized() => Prop.ForAll(
        Generators.SdkLimitOptionsArbitrary(),
        Generators.ResourceArbitrary(),
        Generators.LogRecordSeverityArbitrary(),
        (sdkLimits, resource, severity) =>
        {
            var logRecords = CreateLogRecords(severity);

            try
            {
                var buffer = new byte[10 * 1024 * 1024];
                var batch = new Batch<LogRecord>(logRecords, logRecords.Length);
                var experimentalOptions = new ExperimentalOptions();

                var writePos = ProtobufOtlpLogSerializer.WriteLogsData(
                    ref buffer,
                    0,
                    sdkLimits,
                    experimentalOptions,
                    resource,
                    batch);

                if (writePos <= 0)
                {
                    return true;
                }

                // Try to deserialize
                using var stream = new MemoryStream(buffer, 0, writePos);
                var request = OtlpCollector.ExportLogsServiceRequest.Parser.ParseFrom(stream);

                return request != null && request.ResourceLogs.Count > 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 50)]
    public Property WriteLogsDataHandlesVariousSeverityLevels() => Prop.ForAll(
        Generators.LogRecordSeverityArbitrary(),
        Generators.SdkLimitOptionsArbitrary(),
        (severity, sdkLimits) =>
        {
            var logRecord = Generators.LogRecordArbitrary(severity).Generator.Sample(1).ToArray();

            try
            {
                var buffer = new byte[10 * 1024 * 1024];
                var batch = new Batch<LogRecord>(logRecord, 1);
                var experimentalOptions = new ExperimentalOptions();

                var writePos = ProtobufOtlpLogSerializer.WriteLogsData(
                    ref buffer,
                    0,
                    sdkLimits,
                    experimentalOptions,
                    null,
                    batch);

                return writePos >= 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 50)]
    public Property WriteLogsDataHandlesInvalidSeverities() => Prop.ForAll(
        Gen.Choose(int.MinValue, int.MaxValue).ToArbitrary(),
        Generators.SdkLimitOptionsArbitrary(),
        (severity, sdkLimits) =>
        {
            var logRecord = Generators.LogRecordArbitrary((LogRecordSeverity)severity).Generator.Sample(1).ToArray();

            try
            {
                var buffer = new byte[10 * 1024 * 1024];
                var batch = new Batch<LogRecord>(logRecord, 1);
                var experimentalOptions = new ExperimentalOptions();

                var writePos = ProtobufOtlpLogSerializer.WriteLogsData(
                    ref buffer,
                    0,
                    sdkLimits,
                    experimentalOptions,
                    null,
                    batch);

                return writePos >= 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    private static LogRecord[] CreateLogRecords(LogRecordSeverity severity)
        => Generators.LogRecordArbitrary(severity).Generator.ArrayOf().Sample(1, 10).First();

    private static bool IsAllowedException(Exception ex)
        => ex is IndexOutOfRangeException or ArgumentException;
}
