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
        var state = WriteState.ForWholeBatch();

        return WriteMetricsData(ref buffer, writePosition, resource, batch, out dirtyLength, maxBufferSize, ref state);
    }

    /// <summary>
    /// Writes as many of the metrics of a batch as fit in a request of at most
    /// <paramref name="maxBufferSize"/> bytes, continuing from where the previous
    /// request of the same batch ended.
    /// </summary>
    /// <param name="buffer">The buffer to write to, which is grown as needed up to <paramref name="maxBufferSize"/>.</param>
    /// <param name="writePosition">The position in the buffer to start writing at.</param>
    /// <param name="resource">The resource the metrics belong to.</param>
    /// <param name="batch">The batch of metrics.</param>
    /// <param name="dirtyLength">The number of leading bytes of the buffer which may have been written.</param>
    /// <param name="maxBufferSize">The size the buffer can be grown to, and the position the request is not written past.</param>
    /// <param name="cursor">
    /// Where in the batch to continue from, <see langword="default"/> for its start.
    /// On return, where the next request continues from.
    /// </param>
    /// <param name="completed">
    /// <see langword="true"/> if the request holds the rest of the batch;
    /// otherwise, <see langword="false"/> and the batch needs another request.
    /// </param>
    /// <returns>The position in the buffer after the request.</returns>
    /// <exception cref="IndexOutOfRangeException">The resource, scope and metric which a metric point belongs to do not fit in a request on their own.</exception>
    internal static int WriteMetricsData(
        ref byte[] buffer,
        int writePosition,
        Resources.Resource? resource,
        in Batch<Metric> batch,
        out int dirtyLength,
        int maxBufferSize,
        ref MetricsDataCursor cursor,
        out bool completed)
    {
        var state = WriteState.ForRequest(cursor, maxBufferSize);

        writePosition = WriteMetricsData(ref buffer, writePosition, resource, batch, out dirtyLength, maxBufferSize, ref state);

        cursor = state.Next;
        completed = state.Completed;

        return writePosition;
    }

    private static int WriteMetricsData(
        ref byte[] buffer,
        int writePosition,
        Resources.Resource? resource,
        in Batch<Metric> batch,
        out int dirtyLength,
        int maxBufferSize,
        ref WriteState state)
    {
        dirtyLength = 0;
        metricListPool ??= [];
        scopeMetricsList ??= [];
        scopeMetricsOrder ??= [];

        // Note: The grouped batch is held in thread-static state, so it has to be
        // released even when serialization fails. TryWriteResourceMetrics rethrows
        // once the buffer cannot be grown any further; leaving the batch behind
        // would merge it into the next export on this thread.
        try
        {
            foreach (var metric in batch)
            {
                var metricName = metric.MeterName;
                if (!scopeMetricsList.TryGetValue(metricName, out var metrics))
                {
                    metrics = metricListPool.Count > 0 ? metricListPool.Pop() : [];
                    scopeMetricsList[metricName] = metrics;
                    scopeMetricsOrder.Add(metrics);
                }

                metrics.Add(metric);
            }

            writePosition = TryWriteResourceMetrics(ref buffer, writePosition, resource, scopeMetricsOrder, ref dirtyLength, maxBufferSize, ref state);
        }
        finally
        {
            ReturnMetricListToPool();
        }

        return writePosition;
    }

    private static int TryWriteResourceMetrics(
        ref byte[] buffer,
        int writePosition,
        Resources.Resource? resource,
        List<List<Metric>> scopeMetrics,
        ref int dirtyLength,
        int maxBufferSize,
        ref WriteState state)
    {
        var entryWritePosition = writePosition;

        while (true)
        {
            state.BeginRequest();

            try
            {
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.MetricsData_Resource_Metrics, ProtobufWireType.LEN);
                var metricsDataLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;
                state.Open.ResourceMetrics = metricsDataLengthPosition;

                writePosition = WriteResourceMetrics(buffer, writePosition, resource, scopeMetrics, maxBufferSize, ref dirtyLength, ref state);

                if (!state.Full)
                {
                    // A request continuing a batch whose remaining metric points were all
                    // dropped holds nothing worth sending.
                    if (state.IsEmptyContinuation)
                    {
                        dirtyLength = Math.Max(dirtyLength, writePosition);
                        state.Complete();
                        return entryWritePosition;
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricsDataLengthPosition, writePosition - (metricsDataLengthPosition + ReserveSizeForLength));

                    // Serialization succeeded, return the final write position
                    state.Complete();
                    return writePosition;
                }
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException or ArgumentException)
            {
                // Grow the buffer and start the request over. A request which may be cut
                // is not grown past the size limit, which the pool can already have
                // exceeded when it handed out the buffer, so that the limit is not
                // reported as exceeded every time a request is cut.
                var canGrow = state.SingleRequest || buffer.Length < maxBufferSize;

                if (canGrow && ProtobufSerializer.IncreaseBufferSize(ref buffer, OtlpSignalType.Metrics, maxBufferSize))
                {
                    writePosition = entryWritePosition;
                    dirtyLength = 0;
                    continue;
                }

                // The buffer cannot be grown any further. A whole batch written as a single
                // request cannot be made any smaller, and neither can a request which does
                // not hold a metric point yet, unless the buffer is at the size limit and
                // it is a metric point which does not fit, since the resource, scope and
                // metric it belongs to are repeated in every request.
                if (state.SingleRequest || !(state.HasCheckpoint || (!canGrow && state.WritingMetricPoint)))
                {
                    throw;
                }

                // How far the writer got before running off the end of the buffer is not
                // known, so the whole buffer is dirty.
                dirtyLength = buffer.Length;
            }

            // The buffer is kept, and the bytes written beyond where the request is cut
            // or started over must be cleared before it goes back to the pool.
            dirtyLength = Math.Max(dirtyLength, writePosition);

            // The request is full. Cut it at the end of the last metric point which fits.
            if (state.HasCheckpoint)
            {
                return CutRequest(buffer, ref state);
            }

            // The first metric of the request ended beyond the limit without a metric
            // point being written, so not even its resource, scope and metric fit.
            if (!state.WritingMetricPoint)
            {
                throw new InvalidOperationException($"The metrics could not be serialized within the maximum request size of {maxBufferSize - entryWritePosition} bytes.");
            }

            // The request holds no metric point and the first one to write does not fit,
            // so it is the metric point itself which does not fit in a request on its
            // own. Drop it, and start the request over from the next metric point.
            OpenTelemetryProtocolExporterEventSource.Log.MetricPointDroppedDueToSizeLimit(state.MetricName, maxBufferSize - entryWritePosition);

            state.DropMetricPoint();
            writePosition = entryWritePosition;
        }
    }

    /// <summary>
    /// Cuts a request at its checkpoint, the end of the last metric point which fits,
    /// by discarding what was written after it and closing the messages open there.
    /// </summary>
    /// <param name="buffer">The buffer the request was written to.</param>
    /// <param name="state">The state of the request.</param>
    /// <returns>The position in the buffer after the request.</returns>
    private static int CutRequest(byte[] buffer, ref WriteState state)
    {
        Debug.Assert(state.HasCheckpoint, "A request can only be cut at a checkpoint.");

        int writePosition;
        int scopeMetricsLengthPosition;

        if (state.TrackingMetricPoints)
        {
            // The first metric of the request is cut after the last of its metric
            // points which fits, and the next request continues from the one after.
            writePosition = state.CheckpointPosition;
            scopeMetricsLengthPosition = state.Open.ScopeMetrics;

            if (state.Open.MetricData >= 0)
            {
                ProtobufSerializer.WriteReservedLength(buffer, state.Open.MetricData, writePosition - (state.Open.MetricData + ReserveSizeForLength));
            }

            ProtobufSerializer.WriteReservedLength(buffer, state.Open.Metric, writePosition - (state.Open.Metric + ReserveSizeForLength));

            state.Cut(state.MetricIndex, state.MetricPointIndex, state.CheckpointMetricPoints);
        }
        else if (state.CompletedMetricEndPosition >= 0)
        {
            // A later metric which does not fit is cut at its start, which is the end
            // of the metric before it, and the next request continues from it. The
            // metric may not even have been begun, if the buffer was full to the byte.
            writePosition = state.CompletedMetricEndPosition;
            scopeMetricsLengthPosition = state.Open.ScopeMetrics;

            state.Cut(state.MetricIndex, 0);
        }
        else
        {
            // A scope none of whose metrics is complete is cut at its start, which
            // closes the previous scope of the request, and the next request
            // continues from the first of its metrics not written by the previous
            // requests.
            writePosition = state.ScopeStartPosition;
            scopeMetricsLengthPosition = state.PreviousScopeMetricsLengthPosition;

            state.Cut(Math.Max(state.MetricIndex, state.Resume.MetricIndex), 0);
        }

        var resourceMetricsLengthPosition = state.Open.ResourceMetrics;

        ProtobufSerializer.WriteReservedLength(buffer, scopeMetricsLengthPosition, writePosition - (scopeMetricsLengthPosition + ReserveSizeForLength));
        ProtobufSerializer.WriteReservedLength(buffer, resourceMetricsLengthPosition, writePosition - (resourceMetricsLengthPosition + ReserveSizeForLength));

        return writePosition;
    }

    private static void ReturnMetricListToPool()
    {
        if (scopeMetricsOrder is { Count: > 0 })
        {
            foreach (var metrics in scopeMetricsOrder)
            {
                metrics.Clear();
                metricListPool?.Push(metrics);
            }

            scopeMetricsOrder.Clear();
            scopeMetricsList?.Clear();
        }
    }

    private static int WriteResourceMetrics(
        byte[] buffer,
        int writePosition,
        Resources.Resource? resource,
        List<List<Metric>> scopeMetrics,
        int maxBufferSize,
        ref int dirtyLength,
        ref WriteState state)
    {
        writePosition = ProtobufOtlpResourceSerializer.WriteResource(buffer, writePosition, resource);

        // The schema URL is written ahead of the scopes so that a request cut short
        // after a metric point is complete, apart from the lengths of the open messages.
        if (resource?.SchemaUrl is { Length: > 0 } schemaUrl)
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ResourceMetrics_Schema_Url, schemaUrl);
        }

        return WriteScopeMetrics(buffer, writePosition, scopeMetrics, maxBufferSize, ref dirtyLength, ref state);
    }

    private static int WriteScopeMetrics(
        byte[] buffer,
        int writePosition,
        List<List<Metric>> scopeMetrics,
        int maxBufferSize,
        ref int dirtyLength,
        ref WriteState state)
    {
        if (scopeMetrics != null)
        {
            foreach (var metrics in scopeMetrics)
            {
                // A scope whose metrics were all written by the previous requests.
                if (state.SkipScope(metrics.Count))
                {
                    continue;
                }

                var scopeStartPosition = writePosition;
                writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ResourceMetrics_Scope_Metrics, ProtobufWireType.LEN);
                var resourceMetricsScopeMetricsLengthPosition = writePosition;
                writePosition += ReserveSizeForLength;
                state.BeginScope(scopeStartPosition, resourceMetricsScopeMetricsLengthPosition);

                writePosition = WriteScopeMetric(buffer, writePosition, metrics[0].MeterName, metrics, maxBufferSize, ref dirtyLength, ref state);

                if (state.Full)
                {
                    break;
                }

                // A scope whose only metrics which were not written by the previous
                // requests had nothing left to write is not written either.
                if (state.ScopeMetricsWritten == 0)
                {
                    dirtyLength = Math.Max(dirtyLength, writePosition);
                    writePosition = scopeStartPosition;
                    state.RollBackScope();
                    continue;
                }

                ProtobufSerializer.WriteReservedLength(buffer, resourceMetricsScopeMetricsLengthPosition, writePosition - (resourceMetricsScopeMetricsLengthPosition + ReserveSizeForLength));
            }
        }

        return writePosition;
    }

    private static int WriteScopeMetric(
        byte[] buffer,
        int writePosition,
        string meterName,
        List<Metric> metrics,
        int maxBufferSize,
        ref int dirtyLength,
        ref WriteState state)
    {
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ScopeMetrics_Scope, ProtobufWireType.LEN);
        var instrumentationScopeLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;

        Debug.Assert(metrics.Count > 0, "Metrics collection is not expected to be empty.");
        var metric = metrics[0];
        var meterVersion = metric.MeterVersion;
        var meterTags = metric.MeterTags;
        var meterSchemaUrl = metric.MeterSchemaUrl;

        writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpCommonFieldNumberConstants.InstrumentationScope_Name, meterName);
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

        // The schema URL is written ahead of the metrics so that a request cut short
        // after a metric point is complete, apart from the lengths of the open messages.
        if (!string.IsNullOrEmpty(meterSchemaUrl))
        {
            writePosition = ProtobufSerializer.WriteStringWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ScopeMetrics_Schema_Url, meterSchemaUrl);
        }

        for (var i = 0; i < metrics.Count; i++)
        {
            // A metric written by the previous requests.
            if (state.SkipMetric())
            {
                continue;
            }

            writePosition = WriteMetric(buffer, writePosition, metrics[i], maxBufferSize, ref dirtyLength, ref state);

            if (state.Full)
            {
                break;
            }
        }

        return writePosition;
    }

    private static int WriteMetric(byte[] buffer, int writePosition, Metric metric, int maxBufferSize, ref int dirtyLength, ref WriteState state)
    {
        var metricStartPosition = writePosition;
        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ScopeMetrics_Metrics, ProtobufWireType.LEN);
        var metricLengthPosition = writePosition;
        writePosition += ReserveSizeForLength;
        state.BeginMetric(metric, metricLengthPosition);

        // Do not allocate the cached copy until the destination buffer has grown
        // enough to hold it. ArrayPool may return more than was requested, so the
        // configured maximum must also be applied here.
        var availableBufferSize = Math.Max(0, Math.Min(buffer.Length, maxBufferSize) - writePosition);
        var cachedMetadata = GetOrCreateCachedMetricMetadata(metric, availableBufferSize);
        Buffer.BlockCopy(cachedMetadata, 0, buffer, writePosition, cachedMetadata.Length);
        writePosition += cachedMetadata.Length;

        var aggregationValue = metric.Temporality == AggregationTemporality.Cumulative
            ? ProtobufOtlpMetricFieldNumberConstants.Aggregation_Temporality_Cumulative
            : ProtobufOtlpMetricFieldNumberConstants.Aggregation_Temporality_Delta;

        switch (metric.MetricType)
        {
            case MetricType.LongSum:
            case MetricType.LongSumNonMonotonic:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Sum, ProtobufWireType.LEN);
                    var metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;
                    state.Open.MetricData = metricTypeLengthPosition;

                    writePosition = ProtobufSerializer.WriteBoolWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Is_Monotonic, metric.MetricType == MetricType.LongSum);
                    writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Aggregation_Temporality, aggregationValue);

                    var metricPoints = state.GetMetricPoints(metric);

                    while (metricPoints.MoveNext())
                    {
                        ref readonly var metricPoint = ref metricPoints.Current;
                        state.BeginMetricPoint();

                        var sum = metricPoint.GetSumLong();
                        writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Data_Points, in metricPoint, sum, ref dirtyLength);

                        if (!state.CommitMetricPoint(writePosition, in metricPoints))
                        {
                            break;
                        }
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.DoubleSum:
            case MetricType.DoubleSumNonMonotonic:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Sum, ProtobufWireType.LEN);
                    var metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;
                    state.Open.MetricData = metricTypeLengthPosition;

                    writePosition = ProtobufSerializer.WriteBoolWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Is_Monotonic, metric.MetricType == MetricType.DoubleSum);
                    writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Aggregation_Temporality, aggregationValue);

                    var metricPoints = state.GetMetricPoints(metric);

                    while (metricPoints.MoveNext())
                    {
                        ref readonly var metricPoint = ref metricPoints.Current;
                        state.BeginMetricPoint();

                        var sum = metricPoint.GetSumDouble();
                        writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Sum_Data_Points, in metricPoint, sum, ref dirtyLength);

                        if (!state.CommitMetricPoint(writePosition, in metricPoints))
                        {
                            break;
                        }
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.LongGauge:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Gauge, ProtobufWireType.LEN);
                    var metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;
                    state.Open.MetricData = metricTypeLengthPosition;

                    var metricPoints = state.GetMetricPoints(metric);

                    while (metricPoints.MoveNext())
                    {
                        ref readonly var metricPoint = ref metricPoints.Current;
                        state.BeginMetricPoint();

                        var lastValue = metricPoint.GetGaugeLastValueLong();
                        writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Gauge_Data_Points, in metricPoint, lastValue, ref dirtyLength);

                        if (!state.CommitMetricPoint(writePosition, in metricPoints))
                        {
                            break;
                        }
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.DoubleGauge:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Gauge, ProtobufWireType.LEN);
                    var metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;
                    state.Open.MetricData = metricTypeLengthPosition;

                    var metricPoints = state.GetMetricPoints(metric);

                    while (metricPoints.MoveNext())
                    {
                        ref readonly var metricPoint = ref metricPoints.Current;
                        state.BeginMetricPoint();

                        var lastValue = metricPoint.GetGaugeLastValueDouble();
                        writePosition = WriteNumberDataPoint(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Gauge_Data_Points, in metricPoint, lastValue, ref dirtyLength);

                        if (!state.CommitMetricPoint(writePosition, in metricPoints))
                        {
                            break;
                        }
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.Histogram:
                {
                    // All of the data points of a histogram share the same explicit bounds,
                    // so they are only encoded for the first data point and copied afterwards.
                    var explicitBoundsCache = default(ExplicitBoundsCache);

                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Histogram, ProtobufWireType.LEN);
                    var metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;
                    state.Open.MetricData = metricTypeLengthPosition;

                    writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Histogram_Aggregation_Temporality, aggregationValue);

                    var metricPoints = state.GetMetricPoints(metric);

                    while (metricPoints.MoveNext())
                    {
                        ref readonly var metricPoint = ref metricPoints.Current;
                        state.BeginMetricPoint();

                        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Histogram_Data_Points, ProtobufWireType.LEN);
                        var dataPointLengthPosition = writePosition;
                        writePosition += ReserveSizeForLength;

                        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
                        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Start_Time_Unix_Nano, startTime);

                        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
                        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Time_Unix_Nano, endTime);

                        writePosition = WriteDataPointAttributes(buffer, writePosition, metricPoint.Tags, ProtobufOtlpMetricFieldNumberConstants.HistogramDataPoint_Attributes, ref dirtyLength);

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

                        if (!state.CommitMetricPoint(writePosition, in metricPoints))
                        {
                            break;
                        }
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            case MetricType.ExponentialHistogram:
                {
                    writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.Metric_Data_Exponential_Histogram, ProtobufWireType.LEN);
                    var metricTypeLengthPosition = writePosition;
                    writePosition += ReserveSizeForLength;
                    state.Open.MetricData = metricTypeLengthPosition;

                    writePosition = ProtobufSerializer.WriteEnumWithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogram_Aggregation_Temporality, aggregationValue);

                    var metricPoints = state.GetMetricPoints(metric);

                    while (metricPoints.MoveNext())
                    {
                        ref readonly var metricPoint = ref metricPoints.Current;
                        state.BeginMetricPoint();

                        writePosition = ProtobufSerializer.WriteTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogram_Data_Points, ProtobufWireType.LEN);
                        var dataPointLengthPosition = writePosition;
                        writePosition += ReserveSizeForLength;

                        var startTime = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds();
                        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Start_Time_Unix_Nano, startTime);

                        var endTime = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds();
                        writePosition = ProtobufSerializer.WriteFixed64WithTag(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Time_Unix_Nano, endTime);

                        writePosition = WriteDataPointAttributes(buffer, writePosition, metricPoint.Tags, ProtobufOtlpMetricFieldNumberConstants.ExponentialHistogramDataPoint_Attributes, ref dirtyLength);

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

                        if (!state.CommitMetricPoint(writePosition, in metricPoints))
                        {
                            break;
                        }
                    }

                    ProtobufSerializer.WriteReservedLength(buffer, metricTypeLengthPosition, writePosition - (metricTypeLengthPosition + ReserveSizeForLength));
                    break;
                }

            default:
                break;
        }

        ProtobufSerializer.WriteReservedLength(buffer, metricLengthPosition, writePosition - (metricLengthPosition + ReserveSizeForLength));

        // A metric whose metric points were all written by the previous requests or
        // dropped is not written again, so that a request does not hold a metric
        // with nothing in it, nor spend its size on one ahead of the next metric.
        if (state.IsFirstMetricExhausted)
        {
            dirtyLength = Math.Max(dirtyLength, writePosition);
            state.RollBackMetric();
            return metricStartPosition;
        }

        if (!state.Full)
        {
            state.EndMetric(writePosition);
        }

        return writePosition;
    }

    private static int WriteNumberDataPoint(byte[] buffer, int writePosition, int fieldNumber, in MetricPoint metricPoint, long value, ref int dirtyLength)
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

        writePosition = WriteDataPointAttributes(buffer, writePosition, metricPoint.Tags, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Attributes, ref dirtyLength);

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
        return writePosition;
    }

    private static int WriteNumberDataPoint(byte[] buffer, int writePosition, int fieldNumber, in MetricPoint metricPoint, double value, ref int dirtyLength)
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

        writePosition = WriteDataPointAttributes(buffer, writePosition, metricPoint.Tags, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Attributes, ref dirtyLength);

        writePosition = WriteDoubleExemplars(buffer, writePosition, ProtobufOtlpMetricFieldNumberConstants.NumberDataPoint_Exemplars, in metricPoint, ref dirtyLength);

        ProtobufSerializer.WriteReservedLength(buffer, dataPointLengthPosition, writePosition - (dataPointLengthPosition + ReserveSizeForLength));
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
    /// <returns>The new write position.</returns>
    private static int WriteDataPointAttributes(byte[] buffer, int writePosition, ReadOnlyTagCollection tags, int fieldNumber, ref int dirtyLength)
    {
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

        if (!allTagsWritten)
        {
            // A dropped tag is not necessarily dropped for good (for example a
            // ToString() which throws only sometimes), so an incomplete set of
            // attributes must not be replayed by every later export.
            return writePosition;
        }

        var bytes = new byte[writePosition - startPosition];
        Buffer.BlockCopy(buffer, startPosition, bytes, 0, bytes.Length);

#if NETFRAMEWORK || NETSTANDARD2_0
        CachedDataPointAttributes.Remove(keyAndValues);
        CachedDataPointAttributes.Add(keyAndValues, new CachedAttributes(fieldNumber, bytes));
#else
        CachedDataPointAttributes.AddOrUpdate(keyAndValues, new CachedAttributes(fieldNumber, bytes));
#endif

        return writePosition;
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

    /// <summary>
    /// Where in a batch of metrics the next request continues from, when the batch
    /// is written as several requests.
    /// </summary>
    internal struct MetricsDataCursor
    {
        /// <summary>
        /// The index of the next metric to write, in the order the metrics of the
        /// batch are serialized in, which groups them by scope.
        /// </summary>
        public int MetricIndex;

        /// <summary>
        /// The index of the next metric point of that metric to write.
        /// </summary>
        public int MetricPointIndex;

        /// <summary>
        /// The number of metric points of the batch dropped so far because they did
        /// not fit in a request on their own.
        /// </summary>
        public int DroppedMetricPointCount;

        /// <summary>
        /// The enumerator of the metric points of that metric, positioned at the last
        /// one written or dropped, so that the next request continues from the one
        /// after it without enumerating those before it again. It is only valid when
        /// <see cref="MetricPointIndex"/> is not zero.
        /// </summary>
        public MetricPointsAccessor.Enumerator MetricPoints;
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

    /// <summary>
    /// The positions of the reserved lengths of the messages a metric point is
    /// nested in, or -1 for a message which is not open.
    /// </summary>
    private struct OpenMessages
    {
        public int ResourceMetrics;

        public int ScopeMetrics;

        public int Metric;

        public int MetricData;

        public static OpenMessages None => new() { ResourceMetrics = -1, ScopeMetrics = -1, Metric = -1, MetricData = -1 };
    }

    /// <summary>
    /// Tracks the writing of a request so that it can be cut at the end of the last
    /// metric point which fits when the rest of the batch does not.
    /// </summary>
    /// <remarks>
    /// Only the first metric of a request is tracked metric point by metric point,
    /// as it is the only one which can be too large for a request on its own: a
    /// later metric which does not fit is cut at its start and left whole for the
    /// next request, where it is the first. Likewise a scope which does not fit
    /// before its first metric is complete is cut at its start. This keeps the
    /// tracking of the other metrics and their metric points to a few operations.
    /// </remarks>
    private struct WriteState
    {
        /// <summary>
        /// Whether the whole batch has to be written as this one request, in which
        /// case it is never cut.
        /// </summary>
        public bool SingleRequest;

        /// <summary>
        /// The position the request is not written past. A metric point or metric
        /// which ends beyond it does not fit, even where the buffer is larger.
        /// </summary>
        public int Limit;

        /// <summary>
        /// The metric and metric point the request starts from.
        /// </summary>
        public MetricsDataCursor Resume;

        /// <summary>
        /// Where the next request continues from, once this one is complete or cut.
        /// </summary>
        public MetricsDataCursor Next;

        /// <summary>
        /// Whether the request holds the rest of the batch.
        /// </summary>
        public bool Completed;

        /// <summary>
        /// The index of the metric being written, in the order the metrics are serialized in.
        /// </summary>
        public int MetricIndex;

        /// <summary>
        /// Whether the first metric of the request has been begun.
        /// </summary>
        public bool FirstMetricBegun;

        /// <summary>
        /// Whether the metric being written is the first of the request, whose metric
        /// points are tracked one by one.
        /// </summary>
        public bool TrackingMetricPoints;

        /// <summary>
        /// The index of the metric point of the first metric being written, which
        /// is also the number of its metric points which were written by the
        /// previous requests or dropped.
        /// </summary>
        public int MetricPointIndex;

        /// <summary>
        /// The position of the enumerator of the metric points of the first metric
        /// after the last metric point which fit, where the next request continues
        /// from without enumerating the metric points before it again.
        /// </summary>
        public MetricPointsAccessor.Enumerator CheckpointMetricPoints;

        /// <summary>
        /// The first metric of the request.
        /// </summary>
        public Metric? Metric;

        /// <summary>
        /// Whether a metric point of the first metric is being written, as opposed
        /// to the messages around it.
        /// </summary>
        public bool WritingMetricPoint;

        /// <summary>
        /// Whether a metric point or metric ended beyond <see cref="Limit"/>, so
        /// that the request has to be cut.
        /// </summary>
        public bool Full;

        /// <summary>
        /// The reserved lengths of the messages currently open.
        /// </summary>
        public OpenMessages Open;

        /// <summary>
        /// The position after the last metric point of the first metric which fit,
        /// where the request can be cut while that metric is being written, or -1
        /// when there is none.
        /// </summary>
        public int CheckpointPosition;

        /// <summary>
        /// The position the scope being written starts at, where the request can
        /// be cut while none of its metrics is complete.
        /// </summary>
        public int ScopeStartPosition;

        /// <summary>
        /// The reserved length of the previous scope of the request, which is the
        /// message to close when the request is cut at <see cref="ScopeStartPosition"/>,
        /// or -1 when the scope being written is the first of the request.
        /// </summary>
        public int PreviousScopeMetricsLengthPosition;

        /// <summary>
        /// The number of metrics of the scope being written which are complete.
        /// </summary>
        public int ScopeMetricsWritten;

        /// <summary>
        /// The number of metrics of the request which are complete.
        /// </summary>
        public int MetricsWritten;

        /// <summary>
        /// The position after the last metric of the scope being written which is
        /// complete, where the request can be cut, or -1 while none is.
        /// </summary>
        /// <remarks>
        /// This is also where the metric after it starts, but it is recorded when a
        /// metric is complete rather than when the next one is begun, as the next
        /// one may not be able to start at all when the buffer is full to the byte.
        /// </remarks>
        public int CompletedMetricEndPosition;

        /// <summary>
        /// Gets a value indicating whether the request can be cut somewhere.
        /// </summary>
        public readonly bool HasCheckpoint
            => this.TrackingMetricPoints
                ? this.CheckpointPosition >= 0
                : this.CompletedMetricEndPosition >= 0 || this.PreviousScopeMetricsLengthPosition >= 0;

        /// <summary>
        /// Gets a value indicating whether the request continues a batch from a
        /// previous request and holds no metric, as when the remaining metric
        /// points of the batch were dropped.
        /// </summary>
        public readonly bool IsEmptyContinuation
            => !this.SingleRequest &&
               (this.Resume.MetricIndex > 0 || this.Resume.MetricPointIndex > 0) &&
               this.MetricsWritten == 0;

        /// <summary>
        /// Gets a value indicating whether the first metric of the request has no
        /// metric point left to write, because they were all written by the previous
        /// requests or dropped, and so is not worth sending.
        /// </summary>
        public readonly bool IsFirstMetricExhausted
            => !this.SingleRequest &&
               !this.Full &&
               this.TrackingMetricPoints &&
               this.MetricPointIndex > 0 &&
               this.CheckpointPosition < 0;

        public readonly string MetricName => this.Metric?.Name ?? string.Empty;

        public static WriteState ForWholeBatch()
            => new() { SingleRequest = true, Limit = int.MaxValue };

        public static WriteState ForRequest(in MetricsDataCursor cursor, int limit)
            => new() { Limit = limit, Resume = cursor, Next = cursor };

        public void BeginRequest()
        {
            this.MetricIndex = 0;
            this.FirstMetricBegun = false;
            this.TrackingMetricPoints = false;
            this.WritingMetricPoint = false;
            this.Full = false;
            this.Open = OpenMessages.None;
            this.CheckpointPosition = -1;
            this.PreviousScopeMetricsLengthPosition = -1;
            this.ScopeMetricsWritten = 0;
            this.MetricsWritten = 0;
            this.CompletedMetricEndPosition = -1;
        }

        /// <summary>
        /// Skips a scope if all of its metrics were written by the previous requests.
        /// </summary>
        /// <param name="metricCount">The number of metrics in the scope.</param>
        /// <returns><see langword="true"/> if the scope is skipped; otherwise, <see langword="false"/>.</returns>
        public bool SkipScope(int metricCount)
        {
            if (this.MetricIndex + metricCount <= this.Resume.MetricIndex)
            {
                this.MetricIndex += metricCount;
                return true;
            }

            return false;
        }

        public void BeginScope(int startPosition, int lengthPosition)
        {
            this.PreviousScopeMetricsLengthPosition = this.Open.ScopeMetrics;
            this.ScopeStartPosition = startPosition;
            this.ScopeMetricsWritten = 0;
            this.CompletedMetricEndPosition = -1;
            this.Open.ScopeMetrics = lengthPosition;
        }

        /// <summary>
        /// Rolls back a scope none of whose metrics was written, because the metric
        /// points of those which were not written by the previous requests were dropped.
        /// </summary>
        /// <remarks>
        /// Only the first metric of a request can be rolled back, which is in the
        /// first scope of the request, so there is no previous scope to return to.
        /// </remarks>
        public void RollBackScope()
        {
            // Rolling back any other scope would leave the lengths of the messages
            // open in the request wrong, which would be sent as a corrupt request, so
            // this is checked in every build rather than asserted.
            if (this.PreviousScopeMetricsLengthPosition >= 0 || this.MetricsWritten > 0)
            {
                throw new InvalidOperationException("Only the first scope of a request can be rolled back.");
            }

            this.Open.ScopeMetrics = this.PreviousScopeMetricsLengthPosition;
            this.ScopeMetricsWritten = 0;
            this.CompletedMetricEndPosition = -1;
        }

        /// <summary>
        /// Skips a metric if it was written by the previous requests.
        /// </summary>
        /// <returns><see langword="true"/> if the metric is skipped; otherwise, <see langword="false"/>.</returns>
        public bool SkipMetric()
        {
            if (this.MetricIndex < this.Resume.MetricIndex)
            {
                this.MetricIndex++;
                return true;
            }

            return false;
        }

        public void BeginMetric(Metric metric, int lengthPosition)
        {
            if (!this.FirstMetricBegun)
            {
                this.FirstMetricBegun = true;
                this.TrackingMetricPoints = true;
                this.Metric = metric;
                this.MetricPointIndex = this.MetricIndex == this.Resume.MetricIndex ? this.Resume.MetricPointIndex : 0;
                this.Open.Metric = lengthPosition;
                this.Open.MetricData = -1;
            }
        }

        /// <summary>
        /// Gets the metric points of a metric to write, which for the first metric of
        /// a request are those which were not written by the previous requests.
        /// </summary>
        /// <param name="metric">The metric.</param>
        /// <returns>
        /// An enumerator positioned after the metric points written or dropped
        /// so far, which is not enumerated again from the start.
        /// </returns>
        public readonly MetricPointsAccessor.Enumerator GetMetricPoints(Metric metric)
            => this.TrackingMetricPoints && this.MetricPointIndex > 0
                ? this.Resume.MetricPoints
                : metric.GetMetricPoints().GetEnumerator();

        /// <summary>
        /// Starts a metric point.
        /// </summary>
        public void BeginMetricPoint()
        {
            if (this.TrackingMetricPoints)
            {
                this.WritingMetricPoint = true;
            }
        }

        /// <summary>
        /// Completes a metric point, which becomes the checkpoint the request can be
        /// cut at when the metric is the first of the request.
        /// </summary>
        /// <param name="writePosition">The position after the metric point.</param>
        /// <param name="metricPoints">The enumerator of the metric points of the metric, positioned at the metric point.</param>
        /// <returns><see langword="true"/> if the metric point fits; otherwise, <see langword="false"/> and the request is full.</returns>
        public bool CommitMetricPoint(int writePosition, in MetricPointsAccessor.Enumerator metricPoints)
        {
            if (!this.TrackingMetricPoints)
            {
                return true;
            }

            if (writePosition > this.Limit)
            {
                this.Full = true;
                return false;
            }

            this.WritingMetricPoint = false;
            this.MetricPointIndex++;
            this.CheckpointPosition = writePosition;
            this.CheckpointMetricPoints = metricPoints;
            return true;
        }

        /// <summary>
        /// Completes a metric, if it fits.
        /// </summary>
        /// <param name="writePosition">The position after the metric.</param>
        public void EndMetric(int writePosition)
        {
            if (writePosition > this.Limit)
            {
                this.Full = true;
                return;
            }

            this.TrackingMetricPoints = false;
            this.MetricIndex++;
            this.ScopeMetricsWritten++;
            this.MetricsWritten++;
            this.CompletedMetricEndPosition = writePosition;
        }

        /// <summary>
        /// Rolls back the first metric of the request, none of whose metric points
        /// is left to write, so that the next metric becomes the first of the request.
        /// </summary>
        public void RollBackMetric()
        {
            this.TrackingMetricPoints = false;
            this.FirstMetricBegun = false;
            this.MetricIndex++;
            this.Open.Metric = -1;
            this.Open.MetricData = -1;
        }

        /// <summary>
        /// Drops the metric point being written, so that the request starts over from
        /// the one after it.
        /// </summary>
        public void DropMetricPoint()
        {
            // The metric point is the first to be written by the request, so it is
            // the one after where the previous requests, or the metric points
            // dropped before it, left off.
            // Resuming from the wrong position would write metric points twice or skip
            // them, so this is checked in every build rather than asserted.
            if (this.SingleRequest || !this.TrackingMetricPoints || this.CheckpointPosition >= 0 || this.Metric is null)
            {
                throw new InvalidOperationException("Only the first metric point of a request can be dropped.");
            }

            var metricPoints = this.GetMetricPoints(this.Metric);

            metricPoints.MoveNext();

            this.Resume.MetricIndex = this.MetricIndex;
            this.Resume.MetricPointIndex = this.MetricPointIndex + 1;
            this.Resume.MetricPoints = metricPoints;
            this.Next.DroppedMetricPointCount++;
        }

        public void Cut(int metricIndex, int metricPointIndex)
            => this.Cut(metricIndex, metricPointIndex, default);

        public void Cut(int metricIndex, int metricPointIndex, in MetricPointsAccessor.Enumerator metricPoints)
        {
            this.Next.MetricIndex = metricIndex;
            this.Next.MetricPointIndex = metricPointIndex;
            this.Next.MetricPoints = metricPoints;
            this.Completed = false;
        }

        public void Complete()
        {
            this.Next.MetricIndex = this.MetricIndex;
            this.Next.MetricPointIndex = 0;
            this.Completed = true;
        }
    }

    private sealed class CachedAttributes(int fieldNumber, byte[] bytes)
    {
        public int FieldNumber { get; } = fieldNumber;

        public byte[] Bytes { get; } = bytes;
    }
}
