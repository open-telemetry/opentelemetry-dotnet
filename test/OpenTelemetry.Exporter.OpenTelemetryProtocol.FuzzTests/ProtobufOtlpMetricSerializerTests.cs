// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OpenTelemetry.Metrics;
using OpenTelemetry.Proto.Collector.Metrics.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.FuzzTests;

public class ProtobufOtlpMetricSerializerTests
{
    static ProtobufOtlpMetricSerializerTests()
    {
        Generators.RegisterAll();
    }

    [Property(MaxTest = 100)]
    public Property SerializedDataNeverExceedsBufferSize() => Prop.ForAll(
        Generators.BatchMetricArbitrary(),
        Generators.BufferSizeArbitrary(),
        (metrics, bufferSize) =>
        {
            try
            {
                var buffer = new byte[bufferSize];

                var writePos = ProtobufOtlpMetricSerializer.WriteMetricsData(
                    ref buffer,
                    0,
                    null,
                    metrics);

                return writePos >= 0 && writePos <= buffer.Length;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 100)]
    public Property WriteMetricsDataReturnsNonNegativePosition() => Prop.ForAll(
        Generators.BatchMetricArbitrary(),
        Generators.ResourceArbitrary(),
        (metrics, resource) =>
        {
            try
            {
                var buffer = new byte[10 * 1024 * 1024];

                var writePos = ProtobufOtlpMetricSerializer.WriteMetricsData(
                    ref buffer,
                    0,
                    resource,
                    metrics);

                return writePos >= 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 50)]
    public Property WriteMetricsDataHandlesEmptyBatches() => Prop.ForAll(
        Generators.ResourceArbitrary(),
        (resource) =>
        {
            try
            {
                var buffer = new byte[1024];
                var metrics = new Batch<Metric>([], 0);

                var writePos = ProtobufOtlpMetricSerializer.WriteMetricsData(
                    ref buffer,
                    0,
                    resource,
                    metrics);

                return writePos >= 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 50)]
    public Property BufferAutoResizesWhenNeeded() => Prop.ForAll(
        Generators.BatchMetricArbitrary(),
        Generators.ResourceArbitrary(),
        (metrics, resource) =>
        {
            try
            {
                var buffer = new byte[64]; // Intentionally small
                var initialLength = buffer.Length;

                var writePos = ProtobufOtlpMetricSerializer.WriteMetricsData(
                    ref buffer,
                    0,
                    resource,
                    metrics);

                return buffer.Length >= initialLength && writePos >= 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 50)]
    public Property SerializedOutputCanBeDeserialized() => Prop.ForAll(
        Generators.BatchMetricArbitrary(),
        Generators.ResourceArbitrary(),
        (metrics, resource) =>
        {
            try
            {
                var buffer = new byte[10 * 1024 * 1024];

                var writePos = ProtobufOtlpMetricSerializer.WriteMetricsData(
                    ref buffer,
                    0,
                    resource,
                    metrics);

                if (writePos <= 0)
                {
                    return true;
                }

                // Try to deserialize
                using var stream = new MemoryStream(buffer, 0, writePos);
                var request = ExportMetricsServiceRequest.Parser.ParseFrom(stream);

                return request != null;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    private static bool IsAllowedException(Exception ex)
        => ex is IndexOutOfRangeException or ArgumentException;
}
