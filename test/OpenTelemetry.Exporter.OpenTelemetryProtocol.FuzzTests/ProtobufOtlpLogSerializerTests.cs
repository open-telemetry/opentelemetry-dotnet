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
    static ProtobufOtlpLogSerializerTests()
    {
        Generators.RegisterAll();
    }

    [Property(MaxTest = 100)]
    public Property SerializedDataNeverExceedsBufferSize() => Prop.ForAll(
        Generators.BufferSizeArbitrary(),
        Generators.SdkLimitOptionsArbitrary(),
        (bufferSize, sdkLimits) =>
        {
            var logRecords = CreateLogRecords();

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
            finally
            {
                CleanupLogRecords(logRecords);
            }
        });

    [Property(MaxTest = 100)]
    public Property WriteLogsDataReturnsNonNegativePosition() => Prop.ForAll(
        Generators.SdkLimitOptionsArbitrary(),
        Generators.ResourceArbitrary(),
        (sdkLimits, resource) =>
        {
            var logRecords = CreateLogRecords();

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
            finally
            {
                CleanupLogRecords(logRecords);
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
        (sdkLimits, resource) =>
        {
            var logRecords = CreateLogRecords();

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
            finally
            {
                CleanupLogRecords(logRecords);
            }
        });

    [Property(MaxTest = 50)]
    public Property SerializedOutputCanBeDeserialized() => Prop.ForAll(
        Generators.SdkLimitOptionsArbitrary(),
        Generators.ResourceArbitrary(),
        (sdkLimits, resource) =>
        {
            var logRecords = CreateLogRecords();

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
            catch (Google.Protobuf.InvalidProtocolBufferException)
            {
                return false;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
            finally
            {
                CleanupLogRecords(logRecords);
            }
        });

    [Property(MaxTest = 50)]
    public Property WriteLogsDataHandlesVariousSeverityLevels() => Prop.ForAll(
        Gen.Elements(
            LogRecordSeverity.Trace,
            LogRecordSeverity.Debug,
            LogRecordSeverity.Info,
            LogRecordSeverity.Warn,
            LogRecordSeverity.Error,
            LogRecordSeverity.Fatal).ToArbitrary(),
        Generators.SdkLimitOptionsArbitrary(),
        (severity, sdkLimits) =>
        {
            var logRecord = LogRecordSharedPool.Current.Rent();
            logRecord.Severity = severity;
            logRecord.Timestamp = DateTime.UtcNow;

            try
            {
                var buffer = new byte[10 * 1024 * 1024];
                var batch = new Batch<LogRecord>([logRecord], 1);
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
            finally
            {
                LogRecordSharedPool.Current.Return(logRecord);
            }
        });

    private static LogRecord[] CreateLogRecords()
    {
        var logRecords = new List<LogRecord>();

        for (int i = 0; i < 10; i++)
        {
            var logRecord = LogRecordSharedPool.Current.Rent();

            logRecord.Attributes = [new($"log.attribute.{i}", $"value_{i}")];
            logRecord.Severity = (LogRecordSeverity)(i % 7);
            logRecord.Timestamp = DateTime.UtcNow;

            logRecords.Add(logRecord);
        }

        return [.. logRecords];
    }

    private static void CleanupLogRecords(LogRecord[] logRecords)
    {
        foreach (var logRecord in logRecords)
        {
            LogRecordSharedPool.Current.Return(logRecord);
        }
    }

    private static bool IsAllowedException(Exception ex)
        => ex is IndexOutOfRangeException or ArgumentException;
}
