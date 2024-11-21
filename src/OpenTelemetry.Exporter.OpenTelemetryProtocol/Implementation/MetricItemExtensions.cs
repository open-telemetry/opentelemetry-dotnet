// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Metrics;
using OpenTelemetry.Proto.Metrics.V1;
using AggregationTemporality = OpenTelemetry.Metrics.AggregationTemporality;
using Metric = OpenTelemetry.Metrics.Metric;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpMetrics = OpenTelemetry.Proto.Metrics.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal static class MetricItemExtensions
{
    private static readonly ConcurrentBag<ScopeMetrics> MetricListPool = new();

    internal static void AddMetrics(
        this OtlpCollector.ExportMetricsServiceRequest request,
        OtlpResource.Resource processResource,
        in Batch<Metric> metrics)
    {
        var metricsByLibrary = new Dictionary<string, ScopeMetrics>();
        var resourceMetrics = new ResourceMetrics
        {
            Resource = processResource,
        };
        request.ResourceMetrics.Add(resourceMetrics);

        foreach (var metric in metrics)
        {
            var otlpMetric = metric.ToOtlpMetric();

            // TODO: Replace null check with exception handling.
            if (otlpMetric == null)
            {
                OpenTelemetryProtocolExporterEventSource.Log.CouldNotTranslateMetric(
                    nameof(MetricItemExtensions),
                    nameof(AddMetrics));
                continue;
            }

            var meterName = metric.MeterName;
            if (!metricsByLibrary.TryGetValue(meterName, out var scopeMetrics))
            {
                scopeMetrics = GetMetricListFromPool(meterName, metric.MeterVersion, metric.MeterTags);

                metricsByLibrary.Add(meterName, scopeMetrics);
                resourceMetrics.ScopeMetrics.Add(scopeMetrics);
            }

            scopeMetrics.Metrics.Add(otlpMetric);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Return(this OtlpCollector.ExportMetricsServiceRequest request)
    {
        var resourceMetrics = request.ResourceMetrics.FirstOrDefault();
        if (resourceMetrics == null)
        {
            return;
        }

        foreach (var scopeMetrics in resourceMetrics.ScopeMetrics)
        {
            scopeMetrics.Metrics.Clear();
            scopeMetrics.Scope.Attributes.Clear();
            MetricListPool.Add(scopeMetrics);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ScopeMetrics GetMetricListFromPool(string name, string version, IEnumerable<KeyValuePair<string, object?>>? meterTags)
    {
        if (!MetricListPool.TryTake(out var scopeMetrics))
        {
            scopeMetrics = new ScopeMetrics
            {
                Scope = new OtlpCommon.InstrumentationScope
                {
                    Name = name, // Name is enforced to not be null, but it can be empty.
                    Version = version ?? string.Empty, // NRE throw by proto
                },
            };

            if (meterTags != null)
            {
                AddScopeAttributes(meterTags, scopeMetrics.Scope.Attributes);
            }
        }
        else
        {
            scopeMetrics.Scope.Name = name;
            scopeMetrics.Scope.Version = version ?? string.Empty;
            if (meterTags != null)
            {
                AddScopeAttributes(meterTags, scopeMetrics.Scope.Attributes);
            }
        }

        return scopeMetrics;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OtlpMetrics.Metric ToOtlpMetric(this Metric metric)
    {
        var otlpMetric = new OtlpMetrics.Metric
        {
            Name = metric.Name,
        };

        if (metric.Description != null)
        {
            otlpMetric.Description = metric.Description;
        }

        if (metric.Unit != null)
        {
            otlpMetric.Unit = metric.Unit;
        }

        OtlpMetrics.AggregationTemporality temporality;
        if (metric.Temporality == AggregationTemporality.Delta)
        {
            temporality = OtlpMetrics.AggregationTemporality.Delta;
        }
        else
        {
            temporality = OtlpMetrics.AggregationTemporality.Cumulative;
        }

        switch (metric.MetricType)
        {
            case MetricType.LongSum:
            case MetricType.LongSumNonMonotonic:
                {
                    var sum = new Sum
                    {
                        IsMonotonic = metric.MetricType == MetricType.LongSum,
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsInt = metricPoint.GetSumLong();

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.LongValue, in exemplar));
                            }
                        }

                        sum.DataPoints.Add(dataPoint);

                        if (metric.NoRecordedValueNeeded)
                        {
                            sum.DataPoints.Add(CreateNoRecordedValueNumberDataPoint(dataPoint.TimeUnixNano, metricPoint.Tags));
                        }
                    }

                    otlpMetric.Sum = sum;
                    break;
                }

            case MetricType.DoubleSum:
            case MetricType.DoubleSumNonMonotonic:
                {
                    var sum = new Sum
                    {
                        IsMonotonic = metric.MetricType == MetricType.DoubleSum,
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsDouble = metricPoint.GetSumDouble();

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.DoubleValue, in exemplar));
                            }
                        }

                        sum.DataPoints.Add(dataPoint);

                        if (metric.NoRecordedValueNeeded)
                        {
                            sum.DataPoints.Add(CreateNoRecordedValueNumberDataPoint(dataPoint.TimeUnixNano, metricPoint.Tags));
                        }
                    }

                    otlpMetric.Sum = sum;
                    break;
                }

            case MetricType.LongGauge:
                {
                    var gauge = new Gauge();
                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsInt = metricPoint.GetGaugeLastValueLong();

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.LongValue, in exemplar));
                            }
                        }

                        gauge.DataPoints.Add(dataPoint);

                        if (metric.NoRecordedValueNeeded)
                        {
                            gauge.DataPoints.Add(CreateNoRecordedValueNumberDataPoint(dataPoint.TimeUnixNano, metricPoint.Tags));
                        }
                    }

                    otlpMetric.Gauge = gauge;
                    break;
                }

            case MetricType.DoubleGauge:
                {
                    var gauge = new Gauge();
                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsDouble = metricPoint.GetGaugeLastValueDouble();

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.DoubleValue, in exemplar));
                            }
                        }

                        gauge.DataPoints.Add(dataPoint);

                        if (metric.NoRecordedValueNeeded)
                        {
                            gauge.DataPoints.Add(CreateNoRecordedValueNumberDataPoint(dataPoint.TimeUnixNano, metricPoint.Tags));
                        }
                    }

                    otlpMetric.Gauge = gauge;
                    break;
                }

            case MetricType.Histogram:
                {
                    var histogram = new Histogram
                    {
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new HistogramDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);
                        dataPoint.Count = (ulong)metricPoint.GetHistogramCount();
                        dataPoint.Sum = metricPoint.GetHistogramSum();

                        if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                        {
                            dataPoint.Min = min;
                            dataPoint.Max = max;
                        }

                        foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
                        {
                            dataPoint.BucketCounts.Add((ulong)histogramMeasurement.BucketCount);
                            if (histogramMeasurement.ExplicitBound != double.PositiveInfinity)
                            {
                                dataPoint.ExplicitBounds.Add(histogramMeasurement.ExplicitBound);
                            }
                        }

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.DoubleValue, in exemplar));
                            }
                        }

                        histogram.DataPoints.Add(dataPoint);

                        if (metric.NoRecordedValueNeeded)
                        {
                            histogram.DataPoints.Add(CreateNoRecordedValueHistogramDataPoint(dataPoint.TimeUnixNano, metricPoint.Tags));
                        }
                    }

                    otlpMetric.Histogram = histogram;
                    break;
                }

            case MetricType.ExponentialHistogram:
                {
                    var histogram = new ExponentialHistogram
                    {
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new ExponentialHistogramDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);
                        dataPoint.Count = (ulong)metricPoint.GetHistogramCount();
                        dataPoint.Sum = metricPoint.GetHistogramSum();

                        if (metricPoint.TryGetHistogramMinMaxValues(out double min, out double max))
                        {
                            dataPoint.Min = min;
                            dataPoint.Max = max;
                        }

                        var exponentialHistogramData = metricPoint.GetExponentialHistogramData();
                        dataPoint.Scale = exponentialHistogramData.Scale;
                        dataPoint.ZeroCount = (ulong)exponentialHistogramData.ZeroCount;

                        dataPoint.Positive = new ExponentialHistogramDataPoint.Types.Buckets();
                        dataPoint.Positive.Offset = exponentialHistogramData.PositiveBuckets.Offset;
                        foreach (var bucketCount in exponentialHistogramData.PositiveBuckets)
                        {
                            dataPoint.Positive.BucketCounts.Add((ulong)bucketCount);
                        }

                        if (metricPoint.TryGetExemplars(out var exemplars))
                        {
                            foreach (ref readonly var exemplar in exemplars)
                            {
                                dataPoint.Exemplars.Add(
                                    ToOtlpExemplar(exemplar.DoubleValue, in exemplar));
                            }
                        }

                        histogram.DataPoints.Add(dataPoint);

                        if (metric.NoRecordedValueNeeded)
                        {
                            histogram.DataPoints.Add(CreateNoRecordedValueExponentialHistogramDataPoint(dataPoint.TimeUnixNano, metricPoint.Tags));
                        }
                    }

                    otlpMetric.ExponentialHistogram = histogram;
                    break;
                }
        }

        return otlpMetric;
    }

    internal static OtlpMetrics.Exemplar ToOtlpExemplar<T>(T value, in Metrics.Exemplar exemplar)
        where T : struct
    {
        var otlpExemplar = new OtlpMetrics.Exemplar
        {
            TimeUnixNano = (ulong)exemplar.Timestamp.ToUnixTimeNanoseconds(),
        };

        if (exemplar.TraceId != default)
        {
            byte[] traceIdBytes = new byte[16];
            exemplar.TraceId.CopyTo(traceIdBytes);

            byte[] spanIdBytes = new byte[8];
            exemplar.SpanId.CopyTo(spanIdBytes);

            otlpExemplar.TraceId = UnsafeByteOperations.UnsafeWrap(traceIdBytes);
            otlpExemplar.SpanId = UnsafeByteOperations.UnsafeWrap(spanIdBytes);
        }

        if (typeof(T) == typeof(long))
        {
            otlpExemplar.AsInt = (long)(object)value;
        }
        else if (typeof(T) == typeof(double))
        {
            otlpExemplar.AsDouble = (double)(object)value;
        }
        else
        {
            Debug.Fail("Unexpected type");
            otlpExemplar.AsDouble = Convert.ToDouble(value);
        }

        var otlpExemplarFilteredAttributes = otlpExemplar.FilteredAttributes;

        foreach (var tag in exemplar.FilteredTags)
        {
            OtlpTagWriter.Instance.TryWriteTag(ref otlpExemplarFilteredAttributes, tag);
        }

        return otlpExemplar;
    }

    private static NumberDataPoint CreateNoRecordedValueNumberDataPoint(ulong timestamp, ReadOnlyTagCollection tags)
    {
        var lastDataPoint = new NumberDataPoint
        {
            StartTimeUnixNano = timestamp,
            TimeUnixNano = timestamp,
            Flags = (uint)DataPointFlags.NoRecordedValueMask,
        };

        AddAttributes(tags, lastDataPoint.Attributes);
        return lastDataPoint;
    }

    private static HistogramDataPoint CreateNoRecordedValueHistogramDataPoint(ulong timestamp, ReadOnlyTagCollection tags)
    {
        var lastDataPoint = new HistogramDataPoint
        {
            StartTimeUnixNano = timestamp,
            TimeUnixNano = timestamp,
            Flags = (uint)DataPointFlags.NoRecordedValueMask,
        };

        AddAttributes(tags, lastDataPoint.Attributes);
        return lastDataPoint;
    }

    private static ExponentialHistogramDataPoint CreateNoRecordedValueExponentialHistogramDataPoint(ulong timestamp, ReadOnlyTagCollection tags)
    {
        var lastDataPoint = new ExponentialHistogramDataPoint()
        {
            StartTimeUnixNano = timestamp,
            TimeUnixNano = timestamp,
            Flags = (uint)DataPointFlags.NoRecordedValueMask,
        };

        AddAttributes(tags, lastDataPoint.Attributes);
        return lastDataPoint;
    }

    private static void AddAttributes(ReadOnlyTagCollection tags, RepeatedField<OtlpCommon.KeyValue> attributes)
    {
        foreach (var tag in tags)
        {
            OtlpTagWriter.Instance.TryWriteTag(ref attributes, tag);
        }
    }

    private static void AddScopeAttributes(IEnumerable<KeyValuePair<string, object?>> meterTags, RepeatedField<OtlpCommon.KeyValue> attributes)
    {
        foreach (var tag in meterTags)
        {
            OtlpTagWriter.Instance.TryWriteTag(ref attributes, tag);
        }
    }
}
