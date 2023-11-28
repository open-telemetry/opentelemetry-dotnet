// <copyright file="MetricItemExtensions.cs" company="OpenTelemetry Authors">
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

using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Metrics;
using OtlpCollector = OpenTelemetry.Proto.Collector.Metrics.V1;
using OtlpCommon = OpenTelemetry.Proto.Common.V1;
using OtlpMetrics = OpenTelemetry.Proto.Metrics.V1;
using OtlpResource = OpenTelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation;

internal static class MetricItemExtensions
{
    private static readonly ConcurrentBag<OtlpMetrics.ScopeMetrics> MetricListPool = new();

    internal static void AddMetrics(
        this OtlpCollector.ExportMetricsServiceRequest request,
        OtlpResource.Resource processResource,
        in Batch<Metric> metrics)
    {
        var metricsByLibrary = new Dictionary<string, OtlpMetrics.ScopeMetrics>();
        var resourceMetrics = new OtlpMetrics.ResourceMetrics
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

        foreach (var scope in resourceMetrics.ScopeMetrics)
        {
            scope.Metrics.Clear();
            MetricListPool.Add(scope);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static OtlpMetrics.ScopeMetrics GetMetricListFromPool(string name, string version, IEnumerable<KeyValuePair<string, object>> meterTags)
    {
        if (!MetricListPool.TryTake(out var metrics))
        {
            metrics = new OtlpMetrics.ScopeMetrics
            {
                Scope = new OtlpCommon.InstrumentationScope
                {
                    Name = name, // Name is enforced to not be null, but it can be empty.
                    Version = version ?? string.Empty, // NRE throw by proto
                },
            };
            metrics.Scope.Attributes.Clear();
            AddAttributes(meterTags, metrics.Scope.Attributes);
        }
        else
        {
            metrics.Scope.Name = name;
            metrics.Scope.Version = version ?? string.Empty;
            metrics.Scope.Attributes.Clear();
            AddAttributes(meterTags, metrics.Scope.Attributes);
        }

        return metrics;
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
                    var sum = new OtlpMetrics.Sum
                    {
                        IsMonotonic = metric.MetricType == MetricType.LongSum,
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new OtlpMetrics.NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsInt = metricPoint.GetSumLong();
                        sum.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.Sum = sum;
                    break;
                }

            case MetricType.DoubleSum:
            case MetricType.DoubleSumNonMonotonic:
                {
                    var sum = new OtlpMetrics.Sum
                    {
                        IsMonotonic = metric.MetricType == MetricType.DoubleSum,
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new OtlpMetrics.NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsDouble = metricPoint.GetSumDouble();
                        sum.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.Sum = sum;
                    break;
                }

            case MetricType.LongGauge:
                {
                    var gauge = new OtlpMetrics.Gauge();
                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new OtlpMetrics.NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsInt = metricPoint.GetGaugeLastValueLong();
                        gauge.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.Gauge = gauge;
                    break;
                }

            case MetricType.DoubleGauge:
                {
                    var gauge = new OtlpMetrics.Gauge();
                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new OtlpMetrics.NumberDataPoint
                        {
                            StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                            TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                        };

                        AddAttributes(metricPoint.Tags, dataPoint.Attributes);

                        dataPoint.AsDouble = metricPoint.GetGaugeLastValueDouble();
                        gauge.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.Gauge = gauge;
                    break;
                }

            case MetricType.Histogram:
                {
                    var histogram = new OtlpMetrics.Histogram
                    {
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new OtlpMetrics.HistogramDataPoint
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

                        var exemplars = metricPoint.GetExemplars();
                        foreach (var examplar in exemplars)
                        {
                            if (examplar.Timestamp != default)
                            {
                                byte[] traceIdBytes = new byte[16];
                                examplar.TraceId?.CopyTo(traceIdBytes);

                                byte[] spanIdBytes = new byte[8];
                                examplar.SpanId?.CopyTo(spanIdBytes);

                                var otlpExemplar = new OtlpMetrics.Exemplar
                                {
                                    TimeUnixNano = (ulong)examplar.Timestamp.ToUnixTimeNanoseconds(),
                                    TraceId = UnsafeByteOperations.UnsafeWrap(traceIdBytes),
                                    SpanId = UnsafeByteOperations.UnsafeWrap(spanIdBytes),
                                    AsDouble = examplar.DoubleValue,
                                };

                                if (examplar.FilteredTags != null)
                                {
                                    foreach (var tag in examplar.FilteredTags)
                                    {
                                        if (OtlpKeyValueTransformer.Instance.TryTransformTag(tag, out var result))
                                        {
                                            otlpExemplar.FilteredAttributes.Add(result);
                                        }
                                    }
                                }

                                dataPoint.Exemplars.Add(otlpExemplar);
                            }
                        }

                        histogram.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.Histogram = histogram;
                    break;
                }

            case MetricType.ExponentialHistogram:
                {
                    var histogram = new OtlpMetrics.ExponentialHistogram
                    {
                        AggregationTemporality = temporality,
                    };

                    foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                    {
                        var dataPoint = new OtlpMetrics.ExponentialHistogramDataPoint
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

                        dataPoint.Positive = new OtlpMetrics.ExponentialHistogramDataPoint.Types.Buckets();
                        dataPoint.Positive.Offset = exponentialHistogramData.PositiveBuckets.Offset;
                        foreach (var bucketCount in exponentialHistogramData.PositiveBuckets)
                        {
                            dataPoint.Positive.BucketCounts.Add((ulong)bucketCount);
                        }

                        // TODO: exemplars.

                        histogram.DataPoints.Add(dataPoint);
                    }

                    otlpMetric.ExponentialHistogram = histogram;
                    break;
                }
        }

        return otlpMetric;
    }

    private static void AddAttributes(ReadOnlyTagCollection tags, RepeatedField<OtlpCommon.KeyValue> attributes)
    {
        foreach (var tag in tags)
        {
            if (OtlpKeyValueTransformer.Instance.TryTransformTag(tag, out var result))
            {
                attributes.Add(result);
            }
        }
    }

    private static void AddAttributes(IEnumerable<KeyValuePair<string, object>> meterTags, RepeatedField<OtlpCommon.KeyValue> attributes)
    {
        foreach (var tag in meterTags)
        {
            if (OtlpKeyValueTransformer.Instance.TryTransformTag(tag, out var result))
            {
                attributes.Add(result);
            }
        }
    }

    /*
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static OtlpMetrics.Exemplar ToOtlpExemplar(this IExemplar exemplar)
    {
        var otlpExemplar = new OtlpMetrics.Exemplar();

        if (exemplar.Value is double doubleValue)
        {
            otlpExemplar.AsDouble = doubleValue;
        }
        else if (exemplar.Value is long longValue)
        {
            otlpExemplar.AsInt = longValue;
        }
        else
        {
            // TODO: Determine how we want to handle exceptions here.
            // Do we want to just skip this exemplar and move on?
            // Should we skip recording the whole metric?
            throw new ArgumentException();
        }

        otlpExemplar.TimeUnixNano = (ulong)exemplar.Timestamp.ToUnixTimeNanoseconds();

        // TODO: Do the TagEnumerationState thing.
        foreach (var tag in exemplar.FilteredTags)
        {
            otlpExemplar.FilteredAttributes.Add(tag.ToOtlpAttribute());
        }

        if (exemplar.TraceId != default)
        {
            byte[] traceIdBytes = new byte[16];
            exemplar.TraceId.CopyTo(traceIdBytes);
            otlpExemplar.TraceId = UnsafeByteOperations.UnsafeWrap(traceIdBytes);
        }

        if (exemplar.SpanId != default)
        {
            byte[] spanIdBytes = new byte[8];
            exemplar.SpanId.CopyTo(spanIdBytes);
            otlpExemplar.SpanId = UnsafeByteOperations.UnsafeWrap(spanIdBytes);
        }

        return otlpExemplar;
    }
    */
}
