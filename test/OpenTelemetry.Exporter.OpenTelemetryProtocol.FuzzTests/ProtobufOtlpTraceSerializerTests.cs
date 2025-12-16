// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;
using OtlpCollector = OpenTelemetry.Proto.Collector.Trace.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.FuzzTests;

public class ProtobufOtlpTraceSerializerTests
{
    static ProtobufOtlpTraceSerializerTests()
    {
        Generators.RegisterAll();
        Activity.DefaultIdFormat = ActivityIdFormat.W3C;
        Activity.ForceDefaultIdFormat = true;
    }

    [Property(MaxTest = 200)]
    public Property SerializedDataNeverExceedsBufferSize() => Prop.ForAll(
        Generators.ActivityBatchArbitrary(),
        Generators.SdkLimitOptionsArbitrary(),
        (activities, sdkLimits) =>
        {
            try
            {
                var buffer = new byte[10 * 1024 * 1024]; // 10MB buffer
                var batch = new Batch<Activity>(activities, activities.Length);

                var writePos = ProtobufOtlpTraceSerializer.WriteTraceData(
                    ref buffer,
                    0,
                    sdkLimits,
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
                foreach (var activity in activities)
                {
                    activity?.Dispose();
                }
            }
        });

    [Property(MaxTest = 200)]
    public Property WriteTraceDataReturnsNonNegativePosition() => Prop.ForAll(
        Generators.ActivityBatchArbitrary(),
        Generators.SdkLimitOptionsArbitrary(),
        Generators.ResourceArbitrary(),
        (activities, sdkLimits, resource) =>
        {
            try
            {
                var buffer = new byte[10 * 1024 * 1024];
                var batch = new Batch<Activity>(activities, activities.Length);

                var writePos = ProtobufOtlpTraceSerializer.WriteTraceData(
                    ref buffer,
                    0,
                    sdkLimits,
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
                foreach (var activity in activities)
                {
                    activity?.Dispose();
                }
            }
        });

    [Property(MaxTest = 100)]
    public Property WriteTraceDataHandlesEmptyBatches() => Prop.ForAll(
        Generators.SdkLimitOptionsArbitrary(),
        sdkLimits =>
        {
            try
            {
                var buffer = new byte[1024];
                var batch = new Batch<Activity>([], 0);

                var writePos = ProtobufOtlpTraceSerializer.WriteTraceData(
                    ref buffer,
                    0,
                    sdkLimits,
                    null,
                    batch);

                return writePos >= 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
        });

    [Property(MaxTest = 100)]
    public Property BufferAutoResizesWhenNeeded() => Prop.ForAll(
        Generators.ActivityBatchArbitrary(),
        Generators.SdkLimitOptionsArbitrary(),
        (activities, sdkLimits) =>
        {
            try
            {
                var buffer = new byte[64]; // Intentionally small buffer
                var initialLength = buffer.Length;
                var batch = new Batch<Activity>(activities, activities.Length);

                var writePos = ProtobufOtlpTraceSerializer.WriteTraceData(
                    ref buffer,
                    0,
                    sdkLimits,
                    null,
                    batch);

                // Buffer should either stay same size or grow
                return buffer.Length >= initialLength && writePos >= 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
            finally
            {
                foreach (var activity in activities)
                {
                    activity?.Dispose();
                }
            }
        });

    [Property(MaxTest = 100)]
    public Property SerializedOutputCanBeDeserialized() => Prop.ForAll(
        Generators.ActivityBatchArbitrary(),
        Generators.SdkLimitOptionsArbitrary(),
        Generators.ResourceArbitrary(),
        (activities, sdkLimits, resource) =>
        {
            if (activities == null || activities.Length == 0)
            {
                return true;
            }

            try
            {
                var buffer = new byte[10 * 1024 * 1024];
                var batch = new Batch<Activity>(activities, activities.Length);

                var writePos = ProtobufOtlpTraceSerializer.WriteTraceData(
                    ref buffer,
                    0,
                    sdkLimits,
                    resource,
                    batch);

                if (writePos <= 0)
                {
                    return true;
                }

                // Try to deserialize
                using var stream = new MemoryStream(buffer, 0, writePos);
                var request = OtlpCollector.ExportTraceServiceRequest.Parser.ParseFrom(stream);

                return request != null && request.ResourceSpans.Count > 0;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
            finally
            {
                foreach (var activity in activities)
                {
                    activity?.Dispose();
                }
            }
        });

    [Property(MaxTest = 100)]
    public Property MultipleSerializationCallsAreConsistent() => Prop.ForAll(
        Generators.ActivityBatchArbitrary(),
        Generators.SdkLimitOptionsArbitrary(),
        Generators.ResourceArbitrary(),
        (activities, sdkLimits, resource) =>
        {
            try
            {
                var buffer1 = new byte[10 * 1024 * 1024];
                var buffer2 = new byte[10 * 1024 * 1024];
                var batch = new Batch<Activity>(activities, activities.Length);

                var writePos1 = ProtobufOtlpTraceSerializer.WriteTraceData(
                    ref buffer1,
                    0,
                    sdkLimits,
                    resource,
                    batch);

                // Serialize again with same input
                var writePos2 = ProtobufOtlpTraceSerializer.WriteTraceData(
                    ref buffer2,
                    0,
                    sdkLimits,
                    resource,
                    batch);

                // Positions should match
                if (writePos1 != writePos2)
                {
                    return false;
                }

                // Content should match
                for (int i = 0; i < writePos1; i++)
                {
                    if (buffer1[i] != buffer2[i])
                    {
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex) when (IsAllowedException(ex))
            {
                return true;
            }
            finally
            {
                foreach (var activity in activities)
                {
                    activity?.Dispose();
                }
            }
        });

    private static bool IsAllowedException(Exception ex)
        => ex is IndexOutOfRangeException or ArgumentException;
}
