// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Metrics;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Serializer;

internal static class ProtobufOtlpMetricSerializer
{
    private const int ReserveSizeForLength = 4;
    private const int TraceIdSize = 16;
    private const int SpanIdSize = 8;

#if NETFRAMEWORK || NETSTANDARD2_0
    private static readonly ConditionalWeakTable<Metric, byte[]> CachedMetricMetadata = new();
    private static readonly ConditionalWeakTable<KeyValuePair<string, object?>[], CachedAttributes> CachedDataPointAttributes = new();
#else
    private static readonly ConditionalWeakTable<Metric, byte[]> CachedMetricMetadata = [];
    private static readonly ConditionalWeakTable<KeyValuePair<string, object?>[], CachedAttributes> CachedDataPointAttributes = [];
#endif

    [ThreadStatic]
    private static Stack<List<Metric>>? metricListPool;
    [ThreadStatic]
    private static Dictionary<string, List<Metric>>? scopeMetricsList;

    // The scopes in the order they are serialized in, which is the order of their
    // first metric in the batch. A batch written as several requests is grouped
    // again for each of them, and the cursor between the requests indexes the
    // metrics in this order, so it must not depend on how the dictionary is
    // enumerated.
    [ThreadStatic]
    private static List<List<Metric>>? scopeMetricsOrder;

    private delegate int WriteExemplarFunc(byte[] buffer, int writePosition, in Exemplar exemplar);

    /// <summary>
    /// Writes a whole batch of metrics as a single request.
    /// </summary>
    /// <param name="buffer">The buffer to write to, which is grown as needed up to <paramref name="maxBufferSize"/>.</param>
    /// <param name="writePosition">The position in the buffer to start writing at.</param>
    /// <param name="resource">The resource the metrics belong to.</param>
    /// <param name="batch">The batch of metrics.</param>
    /// <param name="dirtyLength">The number of leading bytes of the buffer which may have been written.</param>
    /// <param name="maxBufferSize">The size the buffer can be grown to.</param>
    /// <returns>The position in the buffer after the request.</returns>
    /// <exception cref="IndexOutOfRangeException">The batch does not fit in a buffer of <paramref name="maxBufferSize"/> bytes.</exception>
    internal static int WriteMetricsData(
        ref byte[] buffer,
        int writePosition,
        Resources.Resource? resource,
        in Batch<Metric> batch,
        out int dirtyLength,
        int maxBufferSize = ProtobufSerializer.MaxBufferSize)
    {
        var cursor = default(MetricsDataCursor);
        var limits = new RequestLimits(maxBufferSize, split: false, writePosition, default);
        return WriteMetricsData(ref buffer, writePosition, resource, batch, out dirtyLength, limits, ref cursor, out _);
    }

    /// <summary>
    /// Writes a request up to the size limit, continuing from the cursor.
    /// </summary>
    /// <param name="buffer">The buffer to write to, grown as needed.</param>
    /// <param name="writePosition">The starting position in the buffer.</param>
    /// <param name="resource">The resource the metrics belong to.</param>
    /// <param name="batch">The batch of metrics.</param>
    /// <param name="dirtyLength">The number of leading bytes that may have been written.</param>
    /// <param name="maxBufferSize">The maximum position in the buffer after writing the request.</param>
    /// <param name="cursor">The position in the batch, advanced past points written or dropped.</param>
    /// <param name="completed">Whether the rest of the batch has been processed.</param>
    /// <param name="deadlineUtc">The deadline to check when dropping points, if specified.</param>
    /// <returns>The position after the request, or the starting position if there is nothing to send.</returns>
    internal static int WriteMetricsData(
        ref byte[] buffer,
        int writePosition,
        Resources.Resource? resource,
        in Batch<Metric> batch,
        out int dirtyLength,
        int maxBufferSize,
        ref MetricsDataCursor cursor,
        out bool completed,
        DateTime deadlineUtc = default)
    {
        var limits = new RequestLimits(maxBufferSize, split: true, writePosition, deadlineUtc);
        return WriteMetricsData(ref buffer, writePosition, resource, batch, out dirtyLength, limits, ref cursor, out completed);
    }

    private static int WriteMetricsData(
        ref byte[] buffer,
        int writePosition,
        Resources.Resource? resource,
        in Batch<Metric> batch,
        out int dirtyLength,
        in RequestLimits limits,
        ref MetricsDataCursor cursor,
        out bool completed)
    {
        metricListPool ??= [];
        scopeMetricsList ??= [];
        scopeMetricsOrder ??= [];

        try
        {
            foreach (var metric in batch)
            {
                var meterName = metric.MeterName;
                if (!scopeMetricsList.TryGetValue(meterName, out var metrics))
                {
                    metrics = metricListPool.Count > 0 ? metricListPool.Pop() : [];
                    scopeMetricsList[meterName] = metrics;
                    scopeMetricsOrder.Add(metrics);
                }

                metrics.Add(metric);
            }

            while (true)
            {
                dirtyLength = 0;
                var next = cursor;

                try
                {
                    var end = WriteResourceMetrics(buffer, writePosition, resource, scopeMetricsOrder, limits, ref dirtyLength, ref next, out completed);
                    cursor = next;
                    return end;
                }
                catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
                {
                    // A larger rental starts the request over at the original cursor.
                    if (!ProtobufSerializer.IncreaseBufferSize(ref buffer, OtlpSignalType.Metrics, limits.MaxBufferSize))
                    {
                        throw;
                    }
                }
            }
        }
        finally
        {
            foreach (var metrics in scopeMetricsOrder)
            {
                metrics.Clear();
                metricListPool.Push(metrics);
            }

            scopeMetricsOrder.Clear();
            scopeMetricsList.Clear();
        }
    }

    private static int WriteResourceMetrics(
        byte[] buffer,
        int requestStart,
        Resources.Resource? resource,
        List<List<Metric>> scopes,
        in RequestLimits limits,
        ref int dirtyLength,
        ref MetricsDataCursor cursor,
        out bool completed)
    {
        var lengthPosition = ProtobufSerializer.WriteTag(buffer, requestStart, ProtobufOtlpMetricFieldNumberConstants.MetricsData_Resource_Metrics, ProtobufWireType.LEN);
        var writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, lengthPosition + ReserveSizeForLength, resource);
        if (resource?.SchemaUrl is { Length: > 0 } schemaUrl)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ResourceMetrics_Schema_Url, schemaUrl);
        }

        limits.EnsureFits(writePosition);
        var scopesStart = writePosition;
        var firstMetricIndex = 0;
        completed = true;

        foreach (var metrics in scopes)
        {
            var start = cursor.MetricIndex - firstMetricIndex;
            if (start < metrics.Count &&
                !WriteScopeMetrics(buffer, ref writePosition, metrics, start, writePosition != scopesStart, limits, ref dirtyLength, ref cursor))
            {
                completed = false;
                break;
            }

            firstMetricIndex += metrics.Count;
        }

        // Keep the original resource-only request for an empty batch.
        if (writePosition == scopesStart && scopes.Count > 0)
        {
            dirtyLength = Math.Max(dirtyLength, writePosition);
            return requestStart;
        }

        ProtobufSerializer.WriteReservedLength(buffer, lengthPosition, writePosition - (lengthPosition + ReserveSizeForLength));
        return writePosition;
    }

    private static bool WriteScopeMetrics(
        byte[] buffer,
        ref int writePosition,
        List<Metric> metrics,
        int start,
        bool requestHasMetrics,
        in RequestLimits limits,
        ref int dirtyLength,
        ref MetricsDataCursor cursor)
    {
        int lengthPosition;
        int metricsStart;

        try
        {
            lengthPosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ResourceMetrics_Scope_Metrics, ProtobufWireType.LEN);
            metricsStart = WriteScopeHeader(buffer, lengthPosition + ReserveSizeForLength, metrics, ref dirtyLength);
            limits.EnsureFits(metricsStart);
        }
        catch (Exception ex) when (requestHasMetrics && limits.CanSplit(buffer, ex))
        {
            dirtyLength = buffer.Length;
            return false;
        }

        var end = metricsStart;
        var completed = true;

        for (var i = start; i < metrics.Count; i++)
        {
            if (!WriteMetric(buffer, ref end, metrics[i], requestHasMetrics || end != metricsStart, limits, ref dirtyLength, ref cursor))
            {
                completed = false;
                break;
            }
        }

        if (end != metricsStart)
        {
            ProtobufSerializer.WriteReservedLength(buffer, lengthPosition, end - (lengthPosition + ReserveSizeForLength));
            writePosition = end;
        }
        else
        {
            dirtyLength = Math.Max(dirtyLength, end);
        }

        return completed;
    }

    private static int WriteScopeHeader(byte[] buffer, int writePosition, List<Metric> metrics, ref int dirtyLength)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ScopeMetrics_Scope, ProtobufWireType.LEN);
        var instrumentationScopeLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        Debug.Assert(metrics.Count > 0, "Metrics collection is not expected to be empty.");
        var metric = metrics[0];
        var meterVersion = metric.MeterVersion;
        var meterTags = metric.MeterTags;
        var meterSchemaUrl = metric.MeterSchemaUrl;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Name, metric.MeterName);
        if (meterVersion != null)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Version, meterVersion);
        }

        if (meterTags != null)
        {
            if (meterTags is IReadOnlyList<KeyValuePair<string, object?>> readonlyMeterTags)
            {
                for (var i = 0; i < readonlyMeterTags.Count; i++)
                {
                    writePosition = WriteTag(buffer, writePosition, readonlyMeterTags[i], ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Attributes, ref dirtyLength);
                }
            }
            else
            {
                foreach (var tag in meterTags)
                {
                    writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Attributes, ref dirtyLength);
                }
            }
        }

        ProtobufSerializer.WriteReservedLength(buffer, instrumentationScopeLengthPosition, writePosition - (instrumentationScopeLengthPosition + ReserveSizeForLength));

        if (!string.IsNullOrEmpty(meterSchemaUrl))
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ScopeMetrics_Schema_Url, meterSchemaUrl);
        }

        return writePosition;
    }

    private static bool WriteMetric(
        byte[] buffer,
        ref int writePosition,
        Metric metric,
        bool requestHasMetrics,
        in RequestLimits limits,
        ref int dirtyLength,
        ref MetricsDataCursor cursor)
    {
        int lengthPosition;
        int dataLengthPosition;
        int pointsStart;

        try
        {
            lengthPosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ScopeMetrics_Metrics, ProtobufWireType.LEN);
            var metadataStart = lengthPosition + ReserveSizeForLength;

            // Bound the cached metadata allocation by both the rental and the configured maximum.
            var available = Math.Max(0, Math.Min(buffer.Length, limits.MaxBufferSize) - metadataStart);
            var metadata = GetOrCreateCachedMetricMetadata(metric, available);
            Buffer.BlockCopy(metadata, 0, buffer, metadataStart, metadata.Length);

            pointsStart = WriteMetricDataHeader(buffer, metadataStart + metadata.Length, metric, out dataLengthPosition);
            limits.EnsureFits(pointsStart);
        }
        catch (Exception ex) when (requestHasMetrics && limits.CanSplit(buffer, ex))
        {
            dirtyLength = buffer.Length;
            return false;
        }

        var metricPoints = cursor.MetricPoints ?? metric.GetMetricPoints().GetEnumerator();
        var includeEmptyMetric = !cursor.MetricPoints.HasValue;
        var explicitBounds = default(ExplicitBoundsCache);
        var end = pointsStart;
        var completed = true;
        var next = metricPoints;

        while (dataLengthPosition >= 0 && next.MoveNext())
        {
            var pointEnd = TryWriteDataPoint(buffer, end, metric.MetricType, in next.Current, limits, ref dirtyLength, ref explicitBounds);
            if (pointEnd > end)
            {
                end = pointEnd;
                metricPoints = next;
                continue;
            }

            if (requestHasMetrics || end != pointsStart)
            {
                // Leave this point for the next request, where the headers are written again.
                cursor.MetricPoints = metricPoints;
                completed = false;
                break;
            }

            OpenTelemetryProtocolExporterEventSource.Log.MetricPointDroppedDueToSizeLimit(metric.Name, limits.End - limits.Start);
            includeEmptyMetric = false;

            // A discarded histogram point may have written bounds that will be overwritten.
            explicitBounds = default;

            if (limits.DeadlineUtc != default && limits.DeadlineUtc <= DateTime.UtcNow)
            {
                throw new TimeoutException("The deadline passed while dropping metric points which do not fit in a request.");
            }
        }

        if (end != pointsStart || (completed && includeEmptyMetric))
        {
            if (dataLengthPosition >= 0)
            {
                ProtobufSerializer.WriteReservedLength(buffer, dataLengthPosition, end - (dataLengthPosition + ReserveSizeForLength));
            }

            ProtobufSerializer.WriteReservedLength(buffer, lengthPosition, end - (lengthPosition + ReserveSizeForLength));
            writePosition = end;
        }
        else
        {
            dirtyLength = Math.Max(dirtyLength, end);
        }

        if (completed)
        {
            cursor.MetricIndex++;
            cursor.MetricPoints = null;
        }

        return completed;
    }

    private static int WriteMetricDataHeader(byte[] buffer, int writePosition, Metric metric, out int lengthPosition)
    {
        var fieldNumber = metric.MetricType switch
        {
            MetricType.LongSum or MetricType.LongSumNonMonotonic or MetricType.DoubleSum or MetricType.DoubleSumNonMonotonic
                => ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Sum,
            MetricType.LongGauge or MetricType.DoubleGauge => ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Gauge,
            MetricType.Histogram => ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Histogram,
            MetricType.ExponentialHistogram => ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Exponential_Histogram,
            _ => 0,
        };

        lengthPosition = -1;
        if (fieldNumber == 0)
        {
            return writePosition;
        }

        lengthPosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        writePosition = lengthPosition + ReserveSizeForLength;
        var aggregationValue = metric.Temporality == AggregationTemporality.Cumulative
            ? ProtobufOtlpMetricFieldNumberConstants.Aggregation_Temporality_Cumulative
            : ProtobufOtlpMetricFieldNumberConstants.Aggregation_Temporality_Delta;

        switch (fieldNumber)
        {
            case ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Sum:
                writePosition = ProtobufSerializer.WriteBoolWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Is_Monotonic, metric.MetricType is MetricType.LongSum or MetricType.DoubleSum);
                return ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Aggregation_Temporality, aggregationValue);
            case ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Histogram:
                return ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Histogram_Aggregation_Temporality, aggregationValue);
            case ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Exponential_Histogram:
                return ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogram_Aggregation_Temporality, aggregationValue);
            default:
                return writePosition;
        }
    }

    private static int TryWriteDataPoint(
        byte[] buffer,
        int writePosition,
        MetricType metricType,
        in MetricPoint point,
        in RequestLimits limits,
        ref int dirtyLength,
        ref ExplicitBoundsCache explicitBounds)
    {
        int end;

        try
        {
            end = metricType switch
            {
                MetricType.LongSum or MetricType.LongSumNonMonotonic
                    => WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Data_Points, in point, point.GetSumLong(), limits.MaxBufferSize, ref dirtyLength),
                MetricType.DoubleSum or MetricType.DoubleSumNonMonotonic
                    => WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Data_Points, in point, point.GetSumDouble(), limits.MaxBufferSize, ref dirtyLength),
                MetricType.LongGauge
                    => WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Gauge_Data_Points, in point, point.GetGaugeLastValueLong(), limits.MaxBufferSize, ref dirtyLength),
                MetricType.DoubleGauge
                    => WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Gauge_Data_Points, in point, point.GetGaugeLastValueDouble(), limits.MaxBufferSize, ref dirtyLength),
                MetricType.Histogram
                    => WriteHistogramDataPoint(buffer, writePosition, in point, limits.MaxBufferSize, ref dirtyLength, ref explicitBounds),
                _ => WriteExponentialHistogramDataPoint(buffer, writePosition, in point, limits.MaxBufferSize, ref dirtyLength),
            };
        }
        catch (Exception ex) when (limits.CanSplit(buffer, ex))
        {
            // The writer may have touched any part of this rental before failing.
            dirtyLength = buffer.Length;
            return writePosition;
        }

        if (end > limits.End)
        {
            dirtyLength = Math.Max(dirtyLength, end);
            return writePosition;
        }

        return end;
    }

    private static int WriteHistogramDataPoint(byte[] buffer, int writePosition, in MetricPoint metricPoint, int maxBufferSize, ref int dirtyLength, ref ExplicitBoundsCache explicitBoundsCache)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Histogram_Data_Points, ProtobufWireType.LEN);
        var dataPointLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Start_Time_Unix_Nano, startTime);

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Time_Unix_Nano, endTime);

        writePosition = WriteDataPointAttributes(buffer, writePosition, metricPoint.Tags, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Attributes, ref dirtyLength, out var attributes);

        var count = (ulong)metricPoint.GetHistogramCount();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Count, count);

        var sum = metricPoint.GetHistogramSum();
        writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Sum, sum);

        if (metricPoint.TryGetHistogramMinMaxValues(out var min, out var max))
        {
            writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Min, min);
            writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Max, max);
        }

        writePosition = WriteHistogramBuckets(buffer, writePosition, metricPoint.GetHistogramBuckets(), ref explicitBoundsCache);

        writePosition = WriteDoubleExemplars(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Exemplars, in metricPoint, ref dirtyLength);

        ProtobufSerializer.WriteReservedLength(buffer, dataPointLengthPosition, writePosition - (dataPointLengthPosition + ReserveSizeForLength));
        CacheDataPointAttributes(buffer, in attributes, writePosition, maxBufferSize);

        return writePosition;
    }

    private static int WriteExponentialHistogramDataPoint(byte[] buffer, int writePosition, in MetricPoint metricPoint, int maxBufferSize, ref int dirtyLength)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogram_Data_Points, ProtobufWireType.LEN);
        var dataPointLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Start_Time_Unix_Nano, startTime);

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Time_Unix_Nano, endTime);

        writePosition = WriteDataPointAttributes(buffer, writePosition, metricPoint.Tags, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Attributes, ref dirtyLength, out var attributes);

        var sum = metricPoint.GetHistogramSum();
        writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Sum, sum);

        var count = (ulong)metricPoint.GetHistogramCount();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Count, count);

        if (metricPoint.TryGetHistogramMinMaxValues(out var min, out var max))
        {
            writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Min, min);
            writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Max, max);
        }

        var exponentialHistogramData = metricPoint.GetExponentialHistogramData();

        writePosition = ProtobufSerializer.WriteSInt32WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Scale, exponentialHistogramData.Scale);
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Zero_Count, (ulong)exponentialHistogramData.ZeroCount);

        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Positive, ProtobufWireType.LEN);
        var positiveBucketsLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        writePosition = ProtobufSerializer.WriteSInt32WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Buckets_Offset, exponentialHistogramData.PositiveBuckets.Offset);

        // The bucket counts are a repeated scalar field, which proto3 packs into a single
        // length-delimited value by default instead of repeating the tag for every bucket.
        // Most data points have few populated buckets, so the length prefix is reserved as
        // a single byte rather than padded out to four.
        var bucketCountsLengthPosition = -1;

        foreach (var bucketCount in exponentialHistogramData.PositiveBuckets)
        {
            if (bucketCountsLengthPosition < 0)
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Buckets_Bucket_Counts, ProtobufWireType.LEN);
                bucketCountsLengthPosition = writePosition;
                writePosition += ProtobufSerializer.ReserveSizeForCompactLength;
            }

            writePosition = ProtobufSerializer.WriteVarInt64(buffer, writePosition, (ulong)bucketCount);
        }

        if (bucketCountsLengthPosition >= 0)
        {
            writePosition = ProtobufSerializer.WriteCompactLength(buffer, bucketCountsLengthPosition, writePosition);
        }

        ProtobufSerializer.WriteReservedLength(buffer, positiveBucketsLengthPosition, writePosition - (positiveBucketsLengthPosition + ReserveSizeForLength));

        writePosition = WriteDoubleExemplars(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Exemplars, in metricPoint, ref dirtyLength);

        ProtobufSerializer.WriteReservedLength(buffer, dataPointLengthPosition, writePosition - (dataPointLengthPosition + ReserveSizeForLength));
        CacheDataPointAttributes(buffer, in attributes, writePosition, maxBufferSize);

        return writePosition;
    }

    private static int WriteNumberDataPoint(byte[] buffer, int writePosition, int fieldNumber, in MetricPoint metricPoint, long value, int maxBufferSize, ref int dirtyLength)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        var dataPointLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        // Casting to ulong is ok here as the bit representation for long versus ulong will be the same
        // The difference would in the way the bit representation is interpreted on decoding side (signed versus unsigned)
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Value_As_Int, (ulong)value);

        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Start_Time_Unix_Nano, startTime);

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Time_Unix_Nano, endTime);

        writePosition = WriteDataPointAttributes(buffer, writePosition, metricPoint.Tags, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Attributes, ref dirtyLength, out var attributes);

        if (metricPoint.TryGetExemplars(out var exemplars))
        {
            foreach (ref readonly var exemplar in exemplars)
            {
                writePosition = WriteExemplar(
                    buffer,
                    writePosition,
                    in exemplar,
                    ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Exemplars,
                    static (buffer, writePosition, in exemplar) => ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Value_As_Int, (ulong)exemplar.LongValue),
                    ref dirtyLength);
            }
        }

        ProtobufSerializer.WriteReservedLength(buffer, dataPointLengthPosition, writePosition - (dataPointLengthPosition + ReserveSizeForLength));
        CacheDataPointAttributes(buffer, in attributes, writePosition, maxBufferSize);
        return writePosition;
    }

    private static int WriteNumberDataPoint(byte[] buffer, int writePosition, int fieldNumber, in MetricPoint metricPoint, double value, int maxBufferSize, ref int dirtyLength)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        var dataPointLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        // Using a func here to avoid boxing/unboxing.
        writePosition = ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Value_As_Double, value);

        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Start_Time_Unix_Nano, startTime);

        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Time_Unix_Nano, endTime);

        writePosition = WriteDataPointAttributes(buffer, writePosition, metricPoint.Tags, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Attributes, ref dirtyLength, out var attributes);

        writePosition = WriteDoubleExemplars(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Exemplars, in metricPoint, ref dirtyLength);

        ProtobufSerializer.WriteReservedLength(buffer, dataPointLengthPosition, writePosition - (dataPointLengthPosition + ReserveSizeForLength));
        CacheDataPointAttributes(buffer, in attributes, writePosition, maxBufferSize);
        return writePosition;
    }

    /// <summary>
    /// Writes the attributes of a data point, reusing the serialized bytes produced for
    /// the same tag array by an earlier export where possible.
    /// </summary>
    /// <remarks>
    /// The cached bytes include the per-attribute field tag, which differs between data
    /// point types, so the field number the entry was built for is validated before the
    /// entry is reused. In practice a tag array only ever belongs to metric points of a
    /// single metric, and therefore to a single data point type, so the check always
    /// passes; it is here so that correctness does not rely on that.
    /// </remarks>
    /// <param name="buffer">The buffer to write to.</param>
    /// <param name="writePosition">The current position in the buffer.</param>
    /// <param name="tags">The data point's tags.</param>
    /// <param name="fieldNumber">The field number to write each attribute under.</param>
    /// <param name="dirtyLength">The furthest position that may have been written, raised by any attribute written past its end.</param>
    /// <param name="pending">
    /// The attributes which were serialized and can be cached once the data point
    /// is known to fit in a request, with <see cref="CacheDataPointAttributes"/>,
    /// or <see langword="default"/> if there is nothing to cache.
    /// </param>
    /// <returns>The new write position.</returns>
    private static int WriteDataPointAttributes(byte[] buffer, int writePosition, ReadOnlyTagCollection tags, int fieldNumber, ref int dirtyLength, out PendingAttributes pending)
    {
        pending = default;

        var keyAndValues = tags.KeyAndValues;

        if (keyAndValues.Length == 0)
        {
            return writePosition;
        }

        if (CachedDataPointAttributes.TryGetValue(keyAndValues, out var cached) &&
            cached.FieldNumber == fieldNumber)
        {
            var cachedBytes = cached.Bytes;
            Buffer.BlockCopy(cachedBytes, 0, buffer, writePosition, cachedBytes.Length);
            return writePosition + cachedBytes.Length;
        }

        var startPosition = writePosition;
        var allTagsWritten = true;

        foreach (var tag in tags)
        {
            writePosition = WriteTag(buffer, writePosition, tag, fieldNumber, out var written, ref dirtyLength);
            allTagsWritten &= written;
        }

        if (allTagsWritten)
        {
            pending = new PendingAttributes(keyAndValues, fieldNumber, startPosition, writePosition - startPosition);
        }

        // Otherwise a dropped tag is not necessarily dropped for good (for example a
        // ToString() which throws only sometimes), so an incomplete set of attributes
        // must not be replayed by every later export.
        return writePosition;
    }

    /// <summary>
    /// Caches the attributes of a data point which were serialized, if the data
    /// point they belong to fits in a request.
    /// </summary>
    /// <remarks>
    /// This is done once the whole data point is written, rather than when its
    /// attributes are, as what follows them (such as buckets and exemplars) can
    /// make it too large for a request. A data point which is dropped would
    /// otherwise have a copy of its attributes retained, and allocated again by
    /// every export of it that follows.
    /// </remarks>
    /// <param name="buffer">The buffer the data point was written to.</param>
    /// <param name="pending">The attributes of the data point.</param>
    /// <param name="writePosition">The position in the buffer after the data point.</param>
    /// <param name="maxBufferSize">The position a request is not written past.</param>
    private static void CacheDataPointAttributes(byte[] buffer, in PendingAttributes pending, int writePosition, int maxBufferSize)
    {
        if (pending.KeyAndValues is not { } keyAndValues || writePosition > maxBufferSize)
        {
            return;
        }

        var bytes = new byte[pending.Length];
        Buffer.BlockCopy(buffer, pending.Start, bytes, 0, bytes.Length);

#if NETFRAMEWORK || NETSTANDARD2_0
        CachedDataPointAttributes.Remove(keyAndValues);
        CachedDataPointAttributes.Add(keyAndValues, new CachedAttributes(pending.FieldNumber, bytes));
#else
        CachedDataPointAttributes.AddOrUpdate(keyAndValues, new CachedAttributes(pending.FieldNumber, bytes));
#endif
    }

    private static int WriteTag(byte[] buffer, int writePosition, KeyValuePair<string, object?> tag, int fieldNumber, ref int dirtyLength)
        => WriteTag(buffer, writePosition, tag, fieldNumber, out _, ref dirtyLength);

    private static int WriteTag(byte[] buffer, int writePosition, KeyValuePair<string, object?> tag, int fieldNumber, out bool written, ref int dirtyLength)
    {
        var otlpTagWriterState = new ProtobufOtlpTagWriter.OtlpTagWriterState
        {
            Buffer = buffer,
            WritePosition = writePosition,
            DirtyLength = dirtyLength,
        };

        written = ProtobufOtlpTagWriter.WriteKeyValue(ref otlpTagWriterState, fieldNumber, tag.Key, tag.Value);

        dirtyLength = otlpTagWriterState.DirtyLength;
        return otlpTagWriterState.WritePosition;
    }

    private static int WriteDoubleExemplars(byte[] buffer, int writePosition, int fieldNumber, in MetricPoint metricPoint, ref int dirtyLength)
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
                    static (buffer, writePosition, in exemplar) => ProtobufSerializer.WriteDoubleWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Value_As_Double, exemplar.DoubleValue),
                    ref dirtyLength);
            }
        }

        return writePosition;
    }

    private static int WriteExemplar(byte[] buffer, int writePosition, in Exemplar exemplar, int fieldNumber, WriteExemplarFunc writeValueFunc, ref int dirtyLength)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, fieldNumber, ProtobufWireType.LEN);
        var exemplarLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        foreach (var tag in exemplar.FilteredTags)
        {
            writePosition = WriteTag(buffer, writePosition, tag, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Filtered_Attributes, ref dirtyLength);
        }

        writePosition = writeValueFunc(buffer, writePosition, in exemplar);

        var time = (ulong)exemplar.Timestamp.ToUnixTimeNanoseconds();
        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Time_Unix_Nano, time);

        if (exemplar.SpanId != default)
        {
            writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, SpanIdSize, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Span_Id, ProtobufWireType.LEN);
            writePosition = ProtobufOtlpTraceSerializer.WriteSpanId(buffer, writePosition, exemplar.SpanId);

            writePosition = ProtobufSerializer.WriteTagAndLength(buffer, writePosition, TraceIdSize, ProtobufOtlpMetricFieldNumberConstants.Exemplar_Trace_Id, ProtobufWireType.LEN);
            writePosition = ProtobufOtlpTraceSerializer.WriteTraceId(buffer, writePosition, exemplar.TraceId);
        }

        ProtobufSerializer.WriteReservedLength(buffer, exemplarLengthPosition, writePosition - (exemplarLengthPosition + ReserveSizeForLength));
        return writePosition;
    }

    private static int WriteHistogramBuckets(byte[] buffer, int writePosition, HistogramBuckets buckets, ref ExplicitBoundsCache explicitBoundsCache)
    {
        writePosition = WriteBucketCounts(buffer, writePosition, buckets.SnapshotBucketCounts);

        if (buckets.ExplicitBounds is { } explicitBounds)
        {
            // Use DisplayBounds (cleaned values) for export if available,
            // otherwise fall back to raw ExplicitBounds
            var bounds = buckets.DisplayBounds ?? explicitBounds;

            if (ReferenceEquals(bounds, explicitBoundsCache.Bounds))
            {
                Buffer.BlockCopy(buffer, explicitBoundsCache.Start, buffer, writePosition, explicitBoundsCache.Length);
                return writePosition + explicitBoundsCache.Length;
            }

            var start = writePosition;
            writePosition = WriteExplicitBounds(buffer, writePosition, bounds);

            if (writePosition > start)
            {
                explicitBoundsCache = new ExplicitBoundsCache(bounds, start, writePosition - start);
            }
        }

        return writePosition;

        static int WriteBucketCounts(byte[] buffer, int writePosition, long[] values)
        {
            var length = values.Length;

            writePosition = WritePackedLength(
                buffer,
                writePosition,
                length,
                ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Bucket_Counts);

            for (var i = 0; i < length; i++)
            {
                writePosition = ProtobufSerializer.WriteFixed64LittleEndianFormat(
                    buffer,
                    writePosition,
                    (ulong)values[i]);
            }

            return writePosition;
        }

        static int WriteExplicitBounds(byte[] buffer, int writePosition, double[] values)
        {
            Debug.Assert(!Array.Exists(values, static value => double.IsInfinity(value)), "Explicit bounds must not contain positive or negative infinity.");

            var length = values.Length;

            if (length > 0)
            {
                writePosition = WritePackedLength(
                    buffer,
                    writePosition,
                    length,
                    ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Explicit_Bounds);

                for (var i = 0; i < values.Length; i++)
                {
                    writePosition = ProtobufSerializer.WriteDouble(buffer, writePosition, values[i]);
                }
            }

            return writePosition;
        }

        static int WritePackedLength(byte[] buffer, int writePosition, int length, int fieldNumber)
        {
            return ProtobufSerializer.WriteTagAndLength(
                buffer,
                writePosition,
                length * 8,
                fieldNumber,
                ProtobufWireType.LEN);
        }
    }

    private static byte[] GetOrCreateCachedMetricMetadata(Metric metric, int availableBufferSize)
    {
        if (!CachedMetricMetadata.TryGetValue(metric, out var cachedMetadata))
        {
#if NET
            cachedMetadata = CachedMetricMetadata.GetOrAdd(
                metric,
                static (key, bufferSize) => SerializeMetricMetadataToBytes(key, bufferSize),
                availableBufferSize);
#else
            // Keep the closure allocation on the cache-miss path on older frameworks.
            static byte[] GetOrAddMetricMetadata(Metric metric, int bufferSize)
            {
                return CachedMetricMetadata.GetOrAdd(
                    metric,
                    key => SerializeMetricMetadataToBytes(key, bufferSize));
            }

            cachedMetadata = GetOrAddMetricMetadata(metric, availableBufferSize);
#endif
        }

        if (cachedMetadata.Length > availableBufferSize)
        {
            throw CreateMetricMetadataTooLargeException(
                nameof(cachedMetadata),
                cachedMetadata.Length,
                availableBufferSize);
        }

        return cachedMetadata;
    }

    private static byte[] SerializeMetricMetadataToBytes(Metric metric, int availableBufferSize)
    {
        var nameUtf8Length = ProtobufSerializer.GetNumberOfUtf8CharsInString(metric.Name);
        var size = ComputeStringWithTagSize(ProtobufOtlpMetricFieldNumberConstants.Metric_Name, nameUtf8Length);

        var descriptionUtf8Length = 0;
        if (metric.Description != null)
        {
            descriptionUtf8Length = ProtobufSerializer.GetNumberOfUtf8CharsInString(metric.Description);
            size += ComputeStringWithTagSize(ProtobufOtlpMetricFieldNumberConstants.Metric_Description, descriptionUtf8Length);
        }

        var unitUtf8Length = 0;
        if (metric.Unit != null)
        {
            unitUtf8Length = ProtobufSerializer.GetNumberOfUtf8CharsInString(metric.Unit);
            size += ComputeStringWithTagSize(ProtobufOtlpMetricFieldNumberConstants.Metric_Unit, unitUtf8Length);
        }

        if (size > availableBufferSize)
        {
            throw CreateMetricMetadataTooLargeException(nameof(size), size, availableBufferSize);
        }

        var buffer = new byte[(int)size];
        var writePosition = ProtobufSerializer.WriteStringWithTag(buffer, 0, ProtobufOtlpMetricFieldNumberConstants.Metric_Name, nameUtf8Length, metric.Name);

        if (metric.Description != null)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Description, descriptionUtf8Length, metric.Description);
        }

        if (metric.Unit != null)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Unit, unitUtf8Length, metric.Unit);
        }

        Debug.Assert(writePosition == size, "writePosition did not match the computed size");
        return buffer;
    }

    private static long ComputeStringWithTagSize(int fieldNumber, int numberOfUtf8Chars) =>
        ProtobufSerializer.ComputeVarInt32Size(ProtobufSerializer.GetTagValue(fieldNumber, ProtobufWireType.LEN)) +
        ProtobufSerializer.ComputeVarInt32Size((uint)numberOfUtf8Chars) +
        (long)numberOfUtf8Chars;

    private static ArgumentOutOfRangeException CreateMetricMetadataTooLargeException(
        string paramName,
        long actualValue,
        int availableBufferSize)
        => new(
            paramName,
            actualValue,
            $"Metric metadata exceeds the available serialization buffer capacity of {availableBufferSize} bytes.");

    internal struct MetricsDataCursor
    {
        // Index in serialization order, grouped by scope.
        public int MetricIndex;

        // Positioned before the next point to write, if resuming within a metric.
        public MetricPointsAccessor.Enumerator? MetricPoints;
    }

    /// <summary>
    /// The serialized attributes of a data point, which are cached once it is known to fit in a request.
    /// </summary>
    private readonly struct PendingAttributes(KeyValuePair<string, object?>[] keyAndValues, int fieldNumber, int start, int length)
    {
        public readonly KeyValuePair<string, object?>[]? KeyAndValues { get; } = keyAndValues;

        public readonly int FieldNumber { get; } = fieldNumber;

        public readonly int Start { get; } = start;

        public readonly int Length { get; } = length;
    }

    /// <summary>
    /// The location in the buffer of the encoded explicit bounds of a histogram data point.
    /// </summary>
    private readonly struct ExplicitBoundsCache(double[] bounds, int start, int length)
    {
        public readonly double[]? Bounds { get; } = bounds;

        public readonly int Start { get; } = start;

        public readonly int Length { get; } = length;
    }

    private readonly struct RequestLimits(int maxBufferSize, bool split, int start, DateTime deadlineUtc)
    {
        public int MaxBufferSize { get; } = maxBufferSize;

        public int End { get; } = split ? maxBufferSize : int.MaxValue;

        public int Start { get; } = start;

        public DateTime DeadlineUtc { get; } = deadlineUtc;

        public bool CanSplit(byte[] buffer, Exception exception)
            => this.End <= buffer.Length && exception is IndexOutOfRangeException or ArgumentException;

        public void EnsureFits(int position)
        {
            if (position > this.End)
            {
                throw new ArgumentOutOfRangeException(nameof(position), position, "The metric request headers exceed the maximum request size.");
            }
        }
    }

    private sealed class CachedAttributes(int fieldNumber, byte[] bytes)
    {
        public int FieldNumber { get; } = fieldNumber;

        public byte[] Bytes { get; } = bytes;
    }
}
