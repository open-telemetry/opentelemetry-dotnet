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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Google.Protobuf;
using Google.Protobuf.Collections;
using OpenTelemetry.Metrics;
using OtlpCollector = Opentelemetry.Proto.Collector.Metrics.V1;
using OtlpCommon = Opentelemetry.Proto.Common.V1;
using OtlpMetrics = Opentelemetry.Proto.Metrics.V1;
using OtlpResource = Opentelemetry.Proto.Resource.V1;

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation
{
    internal static class MetricItemExtensions
    {
        private static readonly ConcurrentBag<OtlpMetrics.InstrumentationLibraryMetrics> MetricListPool = new ConcurrentBag<OtlpMetrics.InstrumentationLibraryMetrics>();
        private static readonly Action<RepeatedField<OtlpMetrics.Metric>, int> RepeatedFieldOfMetricSetCountAction = CreateRepeatedFieldOfMetricSetCountAction();

        internal static void AddBatch(
            this OtlpCollector.ExportMetricsServiceRequest request,
            OtlpResource.Resource processResource,
            in Batch<MetricItem> batch)
        {
            var metricsByLibrary = new Dictionary<string, OtlpMetrics.InstrumentationLibraryMetrics>();
            var resourceMetrics = new OtlpMetrics.ResourceMetrics
            {
                Resource = processResource,
            };
            request.ResourceMetrics.Add(resourceMetrics);

            foreach (var metricItem in batch)
            {
                foreach (var metric in metricItem.Metrics)
                {
                    var otlpMetric = metric.ToOtlpMetric();
                    if (otlpMetric == null)
                    {
                        OpenTelemetryProtocolExporterEventSource.Log.CouldNotTranslateMetric(
                            nameof(MetricItemExtensions),
                            nameof(AddBatch));
                        continue;
                    }

                    var meterName = metric.Meter.Name;
                    if (!metricsByLibrary.TryGetValue(meterName, out var metrics))
                    {
                        metrics = GetMetricListFromPool(meterName, metric.Meter.Version);

                        metricsByLibrary.Add(meterName, metrics);
                        resourceMetrics.InstrumentationLibraryMetrics.Add(metrics);
                    }

                    metrics.Metrics.Add(otlpMetric);
                }
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

            foreach (var libraryMetrics in resourceMetrics.InstrumentationLibraryMetrics)
            {
                RepeatedFieldOfMetricSetCountAction(libraryMetrics.Metrics, 0);
                MetricListPool.Add(libraryMetrics);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static OtlpMetrics.InstrumentationLibraryMetrics GetMetricListFromPool(string name, string version)
        {
            if (!MetricListPool.TryTake(out var metrics))
            {
                metrics = new OtlpMetrics.InstrumentationLibraryMetrics
                {
                    InstrumentationLibrary = new OtlpCommon.InstrumentationLibrary
                    {
                        Name = name, // Name is enforced to not be null, but it can be empty.
                        Version = version ?? string.Empty, // NRE throw by proto
                    },
                };
            }
            else
            {
                metrics.InstrumentationLibrary.Name = name;
                metrics.InstrumentationLibrary.Version = version ?? string.Empty;
            }

            return metrics;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static OtlpMetrics.Metric ToOtlpMetric(this IMetric metric)
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

            if (metric is ISumMetric sumMetric)
            {
                var sum = new OtlpMetrics.Sum
                {
                    IsMonotonic = sumMetric.IsMonotonic,
                    AggregationTemporality = sumMetric.IsDeltaTemporality
                        ? OtlpMetrics.AggregationTemporality.Delta
                        : OtlpMetrics.AggregationTemporality.Cumulative,
                };
                var dataPoint = metric.ToNumberDataPoint(sumMetric.Sum.Value, sumMetric.Exemplars);
                sum.DataPoints.Add(dataPoint);
                otlpMetric.Sum = sum;
            }
            else if (metric is IGaugeMetric gaugeMetric)
            {
                var gauge = new OtlpMetrics.Gauge();
                var dataPoint = metric.ToNumberDataPoint(gaugeMetric.LastValue.Value, gaugeMetric.Exemplars);
                gauge.DataPoints.Add(dataPoint);
                otlpMetric.Gauge = gauge;
            }
            else if (metric is ISummaryMetric summaryMetric)
            {
                var summary = new OtlpMetrics.Summary();

                var dataPoint = new OtlpMetrics.SummaryDataPoint
                {
                    StartTimeUnixNano = (ulong)metric.StartTimeExclusive.ToUnixTimeNanoseconds(),
                    TimeUnixNano = (ulong)metric.EndTimeInclusive.ToUnixTimeNanoseconds(),
                    Count = (ulong)summaryMetric.PopulationCount,
                    Sum = summaryMetric.PopulationSum,
                };

                // TODO: Do TagEnumerationState thing.
                foreach (var attribute in metric.Attributes)
                {
                    dataPoint.Attributes.Add(attribute.ToOtlpAttribute());
                }

                foreach (var quantile in summaryMetric.Quantiles)
                {
                    var quantileValue = new OtlpMetrics.SummaryDataPoint.Types.ValueAtQuantile
                    {
                        Quantile = quantile.Quantile,
                        Value = quantile.Value,
                    };
                    dataPoint.QuantileValues.Add(quantileValue);
                }

                otlpMetric.Summary = summary;
            }
            else if (metric is IHistogramMetric histogramMetric)
            {
                var histogram = new OtlpMetrics.Histogram
                {
                    AggregationTemporality = histogramMetric.IsDeltaTemporality
                        ? OtlpMetrics.AggregationTemporality.Delta
                        : OtlpMetrics.AggregationTemporality.Cumulative,
                };

                var dataPoint = new OtlpMetrics.HistogramDataPoint
                {
                    StartTimeUnixNano = (ulong)metric.StartTimeExclusive.ToUnixTimeNanoseconds(),
                    TimeUnixNano = (ulong)metric.EndTimeInclusive.ToUnixTimeNanoseconds(),
                    Count = (ulong)histogramMetric.PopulationCount,
                    Sum = histogramMetric.PopulationSum,
                };

                foreach (var bucket in histogramMetric.Buckets)
                {
                    dataPoint.BucketCounts.Add((ulong)bucket.Count);

                    // TODO: Verify how to handle the bounds. We've modeled things with
                    // a LowBoundary and HighBoundary. OTLP data model has modeled this
                    // differently: https://github.com/open-telemetry/opentelemetry-proto/blob/bacfe08d84e21fb2a779e302d12e8dfeb67e7b86/opentelemetry/proto/metrics/v1/metrics.proto#L554-L568
                    dataPoint.ExplicitBounds.Add(bucket.HighBoundary);
                }

                // TODO: Do TagEnumerationState thing.
                foreach (var attribute in metric.Attributes)
                {
                    dataPoint.Attributes.Add(attribute.ToOtlpAttribute());
                }

                foreach (var exemplar in histogramMetric.Exemplars)
                {
                    dataPoint.Exemplars.Add(exemplar.ToOtlpExemplar());
                }

                otlpMetric.Histogram = histogram;
            }

            return otlpMetric;
        }

        private static OtlpMetrics.NumberDataPoint ToNumberDataPoint(this IMetric metric, object value, IEnumerable<IExemplar> exemplars)
        {
            var dataPoint = new OtlpMetrics.NumberDataPoint
            {
                StartTimeUnixNano = (ulong)metric.StartTimeExclusive.ToUnixTimeNanoseconds(),
                TimeUnixNano = (ulong)metric.EndTimeInclusive.ToUnixTimeNanoseconds(),
            };

            if (value is double doubleValue)
            {
                dataPoint.AsDouble = doubleValue;
            }
            else if (value is long longValue)
            {
                dataPoint.AsInt = longValue;
            }
            else
            {
                // TODO: Determine how we want to handle exceptions here.
                // Do we want to just skip this metric and move on?
                throw new ArgumentException($"Value must be a long or a double.", nameof(value));
            }

            // TODO: Do TagEnumerationState thing.
            foreach (var attribute in metric.Attributes)
            {
                dataPoint.Attributes.Add(attribute.ToOtlpAttribute());
            }

            foreach (var exemplar in exemplars)
            {
                dataPoint.Exemplars.Add(exemplar.ToOtlpExemplar());
            }

            return dataPoint;
        }

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

        private static Action<RepeatedField<OtlpMetrics.Metric>, int> CreateRepeatedFieldOfMetricSetCountAction()
        {
            FieldInfo repeatedFieldOfMetricCountField = typeof(RepeatedField<OtlpMetrics.Metric>).GetField("count", BindingFlags.NonPublic | BindingFlags.Instance);

            DynamicMethod dynamicMethod = new DynamicMethod(
                "CreateSetCountAction",
                null,
                new[] { typeof(RepeatedField<OtlpMetrics.Metric>), typeof(int) },
                typeof(MetricItemExtensions).Module,
                skipVisibility: true);

            var generator = dynamicMethod.GetILGenerator();

            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Stfld, repeatedFieldOfMetricCountField);
            generator.Emit(OpCodes.Ret);

            return (Action<RepeatedField<OtlpMetrics.Metric>, int>)dynamicMethod.CreateDelegate(typeof(Action<RepeatedField<OtlpMetrics.Metric>, int>));
        }
    }
}
