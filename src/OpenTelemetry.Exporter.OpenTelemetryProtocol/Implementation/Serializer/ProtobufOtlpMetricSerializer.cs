// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpMetricSerializer
{
    private const int ReserveSizeForLength = 4;
    private const int TraceIdSize = 16;
    private const int SpanIdSize = 8;

    private static readonly Stack<List<Metric>> MetricListPool = [];
    private static readonly Dictionary<string, List<Metric>> ScopeMetricsList = [];

    private delegate int WriteExemplarFunc(byte[] buffer, int writePosition, in Exemplar exemplar);

    internal static int WriteMetricsData(
        byte[] buffer,
        int writePosition,
        Resource? resource,
        in Batch<Metric> batch,
        bool emitNoRecordedValueNeededDataPoints)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.MetricsData_Resource_Metrics, ProtobufWireType.LEN);
        int mericsDataLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

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

        writePosition = WriteResourceMetrics(buffer, writePosition, resource, ScopeMetricsList, emitNoRecordedValueNeededDataPoints);
        ProtobufSerializer.WriteReservedLength(buffer, mericsDataLengthPosition, writePosition - (mericsDataLengthPosition + ReserveSizeForLength));
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

    private static int WriteResourceMetrics(
        byte[] buffer,
        int writePosition,
        Resource? resource,
        Dictionary<string, List<Metric>> scopeMetrics,
        bool emitNoRecordedValueNeededDataPoints)
    {
        writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, writePosition, resource);
        writePosition = WriteScopeMetrics(buffer, writePosition, scopeMetrics, emitNoRecordedValueNeededDataPoints);

        return writePosition;
    }

    private static int WriteScopeMetrics(
        byte[] buffer,
        int writePosition,
        Dictionary<string, List<Metric>> scopeMetrics,
        bool emitNoRecordedValueNeededDataPoints)
    {
        foreach (KeyValuePair<string, List<Metric>> entry in scopeMetrics)
        {
            writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ResourceMetrics_Scope_Metrics, ProtobufWireType.LEN);
            int resourceMetricsScopeMetricsLengthPosition = writePosition;
            writePosition += ReserveSizeForLength;

            writePosition = WriteScopeMetric(buffer, writePosition, entry.Key, entry.Value, emitNoRecordedValueNeededDataPoints);

            ProtobufSerializer.WriteReservedLength(buffer, resourceMetricsScopeMetricsLengthPosition, writePosition - (resourceMetricsScopeMetricsLengthPosition + ReserveSizeForLength));
        }

        return writePosition;
    }

    private static int WriteScopeMetric(
        byte[] buffer,
        int writePosition,
        string meterName,
        List<Metric> metrics,
        bool emitNoRecordedValueNeededDataPoints)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ScopeMetrics_Scope, ProtobufWireType.LEN);
        int instrumentationScopeLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        Debug.Assert(metrics.Any(), "Metrics collection is not expected to be empty.");
        var meterVersion = metrics[0].MeterVersion;
        var meterTags = metrics[0].MeterTags;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Name, meterName);
        if (meterVersion != null)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Version, meterVersion);
        }

        if (meterTags != null)
        {
            if (meterTags is IReadOnlyList<KeyValuePair<string, object?>> readonlyMeterTags)
            {
                for (int i = 0; i < readonlyMeterTags.Count; i++)
                {
                    writePosition = WriteTag(buffer, writePosition, readonlyMeterTags[i], ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Attributes);
                }
            }
            else
            {
                foreach (var tag in meterTags)
                {
                    writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Attributes);
                }
            }
        }

        ProtobufSerializer.WriteReservedLength(buffer, instrumentationScopeLengthPosition, writePosition - (instrumentationScopeLengthPosition + ReserveSizeForLength));

        for (int i = 0; i < metrics.Count; i++)
        {
            writePosition = WriteMetric(buffer, writePosition, metrics[i], emitNoRecordedValueNeededDataPoints);
        }

        return writePosition;
    }

    private static int WriteMetric(
        byte[] buffer,
        int writePosition,
        Metric metric,
        bool emitNoRecordedValueNeededDataPoints)
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

        var aggregationValue = metric.Temporality == AggregationTemporality.Cumulative
            ? ProtobufOtlpMetricFieldNumberConstants.Aggregation_Temporality_Cumulative
            : ProtobufOtlpMetricFieldNumberConstants.Aggregation_Temporality_Delta;

        bool isNoRecordedValueNeeded = emitNoRecordedValueNeededDataPoints && metric.NoRecordedValueNeeded;

        switch (metric.MetricType)
        {
            case MetricType.LongSum:
            case MetricType.LongSumNonMonotonic:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Sum, ProtobufWireType.LEN);
                    int metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;

                    writePosition = ProtobufSerializer.WriteBoolWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Is_Monotonic, metric.MetricType == MetricType.LongSum);
                    writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Aggregation_Temporality, aggregationValue);

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var sum = metricPoint.GetSumLong();
                        writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Data_Points, in metricPoint, sum);

                        if (isNoRecordedValueNeeded)
                        {
                            writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Data_Points, in metricPoint, sum, isNoValueRecorded: true);
                        }
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
                    writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Aggregation_Temporality, aggregationValue);

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var sum = metricPoint.GetSumDouble();
                        writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Data_Points, in metricPoint, sum);

                        if (isNoRecordedValueNeeded)
                        {
                            writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Data_Points, in metricPoint, sum, isNoValueRecorded: true);
                        }
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

                        if (isNoRecordedValueNeeded)
                        {
                            writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Gauge_Data_Points, in metricPoint, lastValue, isNoValueRecorded: true);
                        }
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

                        if (isNoRecordedValueNeeded)
                        {
                            writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Gauge_Data_Points, in metricPoint, lastValue, isNoValueRecorded: true);
                        }
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.Histogram:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Histogram, ProtobufWireType.LEN);
                    int metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;

                    writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Histogram_Aggregation_Temporality, aggregationValue);

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        writePosition = WriteHistogramDataPoint(buffer, writePosition, in metricPoint);

                        if (isNoRecordedValueNeeded)
                        {
                            writePosition = WriteHistogramDataPoint(buffer, writePosition, in metricPoint, isNoValueRecorded: true);
                        }
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.ExponentialHistogram:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Exponential_Histogram, ProtobufWireType.LEN);
                    int metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;

                    writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogram_Aggregation_Temporality, aggregationValue);

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        writePosition = WriteExponentialHistogramDataPoint(buffer, writePosition, in metricPoint);

                        if (isNoRecordedValueNeeded)
                        {
                            writePosition = WriteExponentialHistogramDataPoint(buffer, writePosition, in metricPoint, isNoValueRecorded: true);
                        }
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }
        }

        ProtobufSerializer.WriteReservedLength(buffer, metricLengthPosition, writePosition - (metricLengthPosition + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteNumberDataPoint(byte[] buffer, int writePosition, int fieldNumber, in MetricPoint metricPoint, long value, bool isNoValueRecorded = false)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        int dataPointLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        if (!isNoValueRecorded)
        {
            // Casting to ulong is ok here as the bit representation for long versus ulong will be the same
            // The difference would in the way the bit representation is interpreted on decoding side (signed versus unsigned)
            writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Value_As_Int, (ulong)value);

            // No value recorded data point have no aggregation period. They are single point in time markers.
            var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
            writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Start_Time_Unix_Nano, startTime);
        }

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Time_Unix_Nano, endTime);

        foreach (var tag in metricPoint.Tags)
        {
            writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Attributes);
        }

        if (!isNoValueRecorded && metricPoint.TryGetExemplars(out var exemplars))
        {
            foreach (ref readonly var exemplar in exemplars)
            {
                writePosition = WriteExemplar(
                    buffer,
                    writePosition,
                    in exemplar,
                    ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Exemplars,
                    static (byte[] buffer, int writePosition, in Exemplar exemplar) => ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Value_As_Int, (ulong)exemplar.LongValue));
            }
        }

        if (isNoValueRecorded)
        {
            writePosition = ProtobufSerializer.WriteVarInt32WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Flags, ProtobufOtlpMetricFieldNumberConstants.Data_Point_Flags_No_Recorded_Value_Mask);
        }

        ProtobufSerializer.WriteReservedLength(buffer, dataPointLengthPosition, writePosition - (dataPointLengthPosition + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteNumberDataPoint(byte[] buffer, int writePosition, int fieldNumber, in MetricPoint metricPoint, double value, bool isNoValueRecorded = false)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        int dataPointLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        if (!isNoValueRecorded)
        {
            writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Value_As_Double, value);

            // No value recorded data point have no aggregation period. They are single point in time markers.
            var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
            writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Start_Time_Unix_Nano, startTime);
        }

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Time_Unix_Nano, endTime);

        foreach (var tag in metricPoint.Tags)
        {
            writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Attributes);
        }

        if (!isNoValueRecorded)
        {
            writePosition = WriteDoubleExemplars(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Exemplars, in metricPoint);
        }
        else
        {
            writePosition = ProtobufSerializer.WriteVarInt32WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Flags, ProtobufOtlpMetricFieldNumberConstants.Data_Point_Flags_No_Recorded_Value_Mask);
        }

        ProtobufSerializer.WriteReservedLength(buffer, dataPointLengthPosition, writePosition - (dataPointLengthPosition + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteHistogramDataPoint(byte[] buffer, int writePosition, in MetricPoint metricPoint, bool isNoValueRecorded = false)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Histogram_Data_Points, ProtobufWireType.LEN);
        int dataPointLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        if (!isNoValueRecorded)
        {
            // No value recorded data point have no aggregation period. They are single point in time markers.
            var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
            writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Start_Time_Unix_Nano, startTime);
        }

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Time_Unix_Nano, endTime);

        foreach (var tag in metricPoint.Tags)
        {
            writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Attributes);
        }

        if (!isNoValueRecorded)
        {
            var count = (ulong)metricPoint.GetHistogramCount();
            writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Count, count);

            var sum = metricPoint.GetHistogramSum();
            writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Sum, sum);

            if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
            {
                writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Min, min);
                writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Max, max);
            }

            foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
            {
                var bucketCount = (ulong)histogramMeasurement.BucketCount;
                writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Bucket_Counts, bucketCount);

                if (!double.IsPositiveInfinity(histogramMeasurement.ExplicitBound))
                {
                    writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Explicit_Bounds, histogramMeasurement.ExplicitBound);
                }
            }

            writePosition = WriteDoubleExemplars(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Exemplars, in metricPoint);
        }
        else
        {
            writePosition = ProtobufSerializer.WriteVarInt32WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Flags, ProtobufOtlpMetricFieldNumberConstants.Data_Point_Flags_No_Recorded_Value_Mask);
        }

        ProtobufSerializer.WriteReservedLength(buffer, dataPointLengthPosition, writePosition - (dataPointLengthPosition + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteExponentialHistogramDataPoint(byte[] buffer, int writePosition, in MetricPoint metricPoint, bool isNoValueRecorded = false)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogram_Data_Points, ProtobufWireType.LEN);
        int dataPointLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        if (!isNoValueRecorded)
        {
            // No value recorded data point have no aggregation period. They are single point in time markers.
            var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
            writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Start_Time_Unix_Nano, startTime);
        }

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Time_Unix_Nano, endTime);

        foreach (var tag in metricPoint.Tags)
        {
            writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Attributes);
        }

        if (!isNoValueRecorded)
        {
            var sum = metricPoint.GetHistogramSum();
            writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Sum, sum);

            var count = (ulong)metricPoint.GetHistogramCount();
            writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Count, count);

            if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
            {
                writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Min, min);
                writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Max, max);
            }

            var exponentialHistogramData = metricPoint.GetExponentialHistogramData();

            writePosition = ProtobufSerializer.WriteSInt32WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Scale, exponentialHistogramData.Scale);
            writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Zero_Count, (ulong)exponentialHistogramData.ZeroCount);

            writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Positive, ProtobufWireType.LEN);
            int positiveBucketsLengthPosition = writePosition;
            writePosition += ReserveSizeForLength;

            writePosition = ProtobufSerializer.WriteSInt32WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Buckets_Offset, exponentialHistogramData.PositiveBuckets.Offset);

            foreach (var bucketCount in exponentialHistogramData.PositiveBuckets)
            {
                writePosition = ProtobufSerializer.WriteInt64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Buckets_Bucket_Counts, (ulong)bucketCount);
            }

            ProtobufSerializer.WriteReservedLength(buffer, positiveBucketsLengthPosition, writePosition - (positiveBucketsLengthPosition + ReserveSizeForLength));

            writePosition = WriteDoubleExemplars(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Exemplars, in metricPoint);
        }
        else
        {
            writePosition = ProtobufSerializer.WriteVarInt32WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Flags, ProtobufOtlpMetricFieldNumberConstants.Data_Point_Flags_No_Recorded_Value_Mask);
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

    private static int WriteDoubleExemplars(byte[] buffer, int writePosition, int fieldNumber, in MetricPoint metricPoint)
    {
        if (metricPoint.TryGetExemplars(out var exemplars))
        {
            foreach (ref readonly var exemplar in exemplars)
            {
                writePosition = WriteExemplar(
                    buffer,
                    writePosition,
                    in exemplar,
                    fieldNumber,
                    static (byte[] buffer, int writePosition, in Exemplar exemplar) => ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Value_As_Double, exemplar.DoubleValue));
            }
        }

        return writePosition;
    }

    private static int WriteExemplar(byte[] buffer, int writePosition, in Exemplar exemplar, int fieldNumber, WriteExemplarFunc writeValueFunc)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        int exemplarLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        foreach (var tag in exemplar.FilteredTags)
        {
            writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Filtered_Attributes);
        }

        writePosition = writeValueFunc(buffer, writePosition, in exemplar);

        var time = (ulong)exemplar.Timestamp.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Time_Unix_Nano, time);

        if (exemplar.SpanId != default)
        {
            writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, SpanIdSize, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Span_Id, ProtobufWireType.LEN);
            var spanIdBytes = new Span<byte>(buffer, writePosition, SpanIdSize);
            exemplar.SpanId.CopyTo(spanIdBytes);
            writePosition += SpanIdSize;

            writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, TraceIdSize, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Trace_Id, ProtobufWireType.LEN);
            var traceIdBytes = new Span<byte>(buffer, writePosition, TraceIdSize);
            exemplar.TraceId.CopyTo(traceIdBytes);
            writePosition += TraceIdSize;
        }

        ProtobufSerializer.WriteReservedLength(buffer, exemplarLengthPosition, writePosition - (exemplarLengthPosition + ReserveSizeForLength));
        return writePosition;
    }
}
