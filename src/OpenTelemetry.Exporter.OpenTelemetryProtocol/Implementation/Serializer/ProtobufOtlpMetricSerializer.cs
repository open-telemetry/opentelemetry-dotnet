// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpMetricSerializer
{
    private const int ReserveSizeForLength = 4;
    private const int TraceIdSize = 16;
    private const int SpanIdSize = 8;

    private static readonly Stack<List<Metric>> MetricListPool = [];
    private static readonly Dictionary<string, List<Metric>> ScopeMetricsList = [];

    internal static int WriteMetricsData(byte[] buffer, int writePosition, Resources.Resource? resource, in Batch<Metric> batch)
    {
        foreach (var metric in batch)
        {
            var metricName = metric.MeterName;
            if (!ScopeMetricsList.TryGetValue(metricName, out var metrics))
            {
                metrics = MetricListPool.Count > 0 ? MetricListPool.Pop() : new List<Metric>();
                ScopeMetricsList[metricName] = metrics;
            }

            metrics.Add(metric);
        }

        writePosition = WriteResourceMetrics(buffer, writePosition, resource, ScopeMetricsList);
        ReturnMetricListToPool();

        return writePosition;
    }

    private static void ReturnMetricListToPool()
    {
        if (ScopeMetricsList.Count != 0)
        {
            foreach (var entry in ScopeMetricsList)
            {
                entry.Value.Clear();
                MetricListPool.Push(entry.Value);
            }

            ScopeMetricsList.Clear();
        }
    }

    private static int WriteResourceMetrics(byte[] buffer, int writePosition, Resources.Resource? resource, Dictionary<string, List<Metric>> scopeMetrics)
    {
        writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, writePosition, resource);
        writePosition = WriteScopeMetrics(buffer, writePosition, scopeMetrics);

        return writePosition;
    }

    private static int WriteScopeMetrics(byte[] buffer, int writePosition, Dictionary<string, List<Metric>> scopeMetrics)
    {
        if (scopeMetrics != null)
        {
            foreach (KeyValuePair<string, List<Metric>> entry in scopeMetrics)
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ResourceMetrics_Scope_Metrics, ProtobufWireType.LEN);
                int resourceMetricsScopeMetricsLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;

                writePosition = WriteScopeMetric(buffer, writePosition, entry.Value);

                ProtobufSerializer.WriteReservedLength(buffer, resourceMetricsScopeMetricsLengthPosition, writePosition - (resourceMetricsScopeMetricsLengthPosition + ReserveSizeForLength));
            }
        }

        return writePosition;
    }

    private static int WriteScopeMetric(byte[] buffer, int writePosition, List<Metric> metrics)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ScopeMetrics_Scope, ProtobufWireType.LEN);
        int instrumentationScopeLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        Debug.Assert(metrics.Any(), "Metrics collection is not expected to be empty.");
        var meterName = metrics[0].MeterName;
        var meterVersion = metrics[0].MeterVersion;
        var meterTags = metrics[0].MeterTags;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.InstrumentationScope_Name, meterName);
        if (meterVersion != null)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.InstrumentationScope_Version, meterVersion);
        }

        if (meterTags != null)
        {
            // TODO: DON'T HAVE ANY TESTS THAT EVALUATE THIS

            if (meterTags is IReadOnlyList<KeyValuePair<string, object?>> readonlyMeterTags)
            {
                for (int i = 0; i < readonlyMeterTags.Count; i++)
                {
                    writePosition = WriteTag(buffer, writePosition, readonlyMeterTags[i], ProtobufOtlpMetricFieldNumberConstants.InstrumentationScope_Attributes);
                }
            }
            else
            {
                foreach (var tag in meterTags)
                {
                    writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpMetricFieldNumberConstants.InstrumentationScope_Attributes);
                }
            }
        }

        ProtobufSerializer.WriteReservedLength(buffer, instrumentationScopeLengthPosition, writePosition - (instrumentationScopeLengthPosition + ReserveSizeForLength));

        for (int i = 0; i < metrics.Count; i++)
        {
            writePosition = WriteMetric(buffer, writePosition, metrics[i]);
        }

        return writePosition;
    }

    private static int WriteMetric(byte[] buffer, int writePosition, Metric metric)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ScopeMetrics_Metrics, ProtobufWireType.LEN);
        int metricLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Name, metric.Name);

        if (metric.Description != null)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Description, metric.Description);
        }

        if (metric.Unit != null)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Unit, metric.Unit);
        }

        switch (metric.MetricType)
        {
            case MetricType.LongSum:
            case MetricType.LongSumNonMonotonic:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Sum, ProtobufWireType.LEN);
                    int metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;

                    writePosition = ProtobufSerializer.WriteBoolWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Is_Monotonic, metric.MetricType == MetricType.LongSum);
                    writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Aggregation_Temporality, metric.Temporality == AggregationTemporality.Cumulative ? 2 : 1);

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var sum = metricPoint.GetSumLong();
                        writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Data_Points, in metricPoint, sum);
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.DoubleSum:
            case MetricType.DoubleSumNonMonotonic:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Sum, ProtobufWireType.LEN);
                    int metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;

                    writePosition = ProtobufSerializer.WriteBoolWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Is_Monotonic, metric.MetricType == MetricType.DoubleSum);
                    writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Aggregation_Temporality, metric.Temporality == AggregationTemporality.Cumulative ? 2 : 1);

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var sum = metricPoint.GetSumDouble();
                        writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Data_Points, in metricPoint, sum);
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.LongGauge:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Gauge, ProtobufWireType.LEN);
                    int metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var lastValue = metricPoint.GetGaugeLastValueLong();
                        writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Gauge_Data_Points, in metricPoint, lastValue);
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.DoubleGauge:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Gauge, ProtobufWireType.LEN);
                    int metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var lastValue = metricPoint.GetGaugeLastValueDouble();
                        writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Gauge_Data_Points, in metricPoint, lastValue);
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.Histogram:
                {
                    break;
                }

            case MetricType.ExponentialHistogram:
                {
                    break;
                }
        }

        ProtobufSerializer.WriteReservedLength(buffer, metricLengthPosition, writePosition - (metricLengthPosition + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteNumberDataPoint(byte[] buffer, int writePosition, int fieldNumber, in MetricPoint metricPoint, long value)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        int dataPointLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        // Casting to ulong is ok here as the bit representation for long versus ulong will be the same
        // The difference would in the way the bit representation is interpreted on decoding side (signed versus unsigned)
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Value_As_Int, (ulong)value);

        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Start_Time_Unix_Nano, startTime);

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Time_Unix_Nano, endTime);

        foreach (var tag in metricPoint.Tags)
        {
            writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Attributes);
        }

        if (metricPoint.TryGetExemplars(out var exemplars))
        {
            foreach (ref readonly var exemplar in exemplars)
            {
                writePosition = WriteExemplar(buffer, writePosition, in exemplar, exemplar.LongValue, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Exemplars);
            }
        }

        ProtobufSerializer.WriteReservedLength(buffer, dataPointLengthPosition, writePosition - (dataPointLengthPosition + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteNumberDataPoint(byte[] buffer, int writePosition, int fieldNumber, in MetricPoint metricPoint, double value)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        int dataPointLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        // Using a func here to avoid boxing/unboxing.
        writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Value_As_Double, value);

        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Start_Time_Unix_Nano, startTime);

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Time_Unix_Nano, endTime);

        foreach (var tag in metricPoint.Tags)
        {
            writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Attributes);
        }

        if (metricPoint.TryGetExemplars(out var exemplars))
        {
            foreach (ref readonly var exemplar in exemplars)
            {
                writePosition = WriteExemplar(buffer, writePosition, in exemplar, exemplar.DoubleValue, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Exemplars);
            }
        }

        ProtobufSerializer.WriteReservedLength(buffer, dataPointLengthPosition, writePosition - (dataPointLengthPosition + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteTag(byte[] buffer, int writePosition, KeyValuePair<string, object?> tag, int fieldNumber)
    {
        ProtobufOtlpTagWriter.OtlpTagWriterState otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
        };

        otlpTagWriterState.WritePosition = ProtobufSerializer.WriteTag(buffer, otlpTagWriterState.WritePosition, fieldNumber, ProtobufWireType.LEN);
        int fieldLengthPosition = otlpTagWriterState.WritePosition;
        otlpTagWriterState.WritePosition += ReserveSizeForLength;

        ProtobufOtlpTagWriter.Instance.TryWriteTag(ref otlpTagWriterState, tag.Key, tag.Value);

        ProtobufSerializer.WriteReservedLength(buffer, fieldLengthPosition, otlpTagWriterState.WritePosition - (fieldLengthPosition + ReserveSizeForLength));
        return otlpTagWriterState.WritePosition;
    }

    private static int WriteExemplar(byte[] buffer, int writePosition, in Exemplar exemplar, long value, int fieldNumber)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        int exemplarLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        // TODO: DON'T HAVE ANY TESTS THAT EVALUATE THIS
        // SerializeExemplarTags(buffer, ref cursor, exemplar.FilteredTags);

        // Casting to ulong is ok here as the bit representation for long versus ulong will be the same
        // The difference would in the way the bit representation is interpreted on decoding side (signed versus unsigned)
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Value_As_Int, (ulong)value);

        var time = (ulong)exemplar.Timestamp.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Time_Unix_Nano, time);

        if (exemplar.SpanId != default)
        {
            // TODO: DON'T HAVE ANY TESTS THAT EVALUATE THIS
            // ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref cursor, TraceIdSize, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Trace_Id, ProtobufWireType.LEN);
            // var traceBytes = new Span<byte>(buffer, cursor, TraceIdSize);
            // exemplar.TraceId.CopyTo(traceBytes);
            // cursor += TraceIdSize;
            // ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref cursor, SpanIdSize, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Span_Id, ProtobufWireType.LEN);
            // var spanBytes = new Span<byte>(buffer, cursor, SpanIdSize);
            // exemplar.SpanId.CopyTo(spanBytes);
            // cursor += SpanIdSize;
        }

        ProtobufSerializer.WriteReservedLength(buffer, exemplarLengthPosition, writePosition - (exemplarLengthPosition + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteExemplar(byte[] buffer, int writePosition, in Exemplar exemplar, double value, int fieldNumber)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        int exemplarLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        // TODO: DON'T HAVE ANY TESTS THAT EVALUATE THIS
        // SerializeExemplarTags(buffer, ref cursor, exemplar.FilteredTags);

        writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Value_As_Double, value);

        var time = (ulong)exemplar.Timestamp.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Time_Unix_Nano, time);

        if (exemplar.SpanId != default)
        {
            // TODO: DON'T HAVE ANY TESTS THAT EVALUATE THIS
            // ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref cursor, TraceIdSize, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Trace_Id, ProtobufWireType.LEN);
            // var traceBytes = new Span<byte>(buffer, cursor, TraceIdSize);
            // exemplar.TraceId.CopyTo(traceBytes);
            // cursor += TraceIdSize;
            // ProtobufSerializerHelper.WriteTagAndLengthPrefix(buffer, ref cursor, SpanIdSize, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Span_Id, ProtobufWireType.LEN);
            // var spanBytes = new Span<byte>(buffer, cursor, SpanIdSize);
            // exemplar.SpanId.CopyTo(spanBytes);
            // cursor += SpanIdSize;
        }

        ProtobufSerializer.WriteReservedLength(buffer, exemplarLengthPosition, writePosition - (exemplarLengthPosition + ReserveSizeForLength));
        return writePosition;
    }
}
