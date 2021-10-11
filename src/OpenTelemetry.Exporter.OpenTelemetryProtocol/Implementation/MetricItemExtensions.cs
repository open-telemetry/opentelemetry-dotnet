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

        internal static void AddMetrics(
            this OtlpCollector.ExportMetricsServiceRequest request,
            OtlpResource.Resource processResource,
            in Batch<Metric> metrics)
        {
            var metricsByLibrary = new Dictionary<string, OtlpMetrics.InstrumentationLibraryMetrics>();
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

                var meterName = metric.Meter.Name;
                if (!metricsByLibrary.TryGetValue(meterName, out var instrumentationLibraryMetrics))
                {
                    instrumentationLibraryMetrics = GetMetricListFromPool(meterName, metric.Meter.Version);

                    metricsByLibrary.Add(meterName, instrumentationLibraryMetrics);
                    resourceMetrics.InstrumentationLibraryMetrics.Add(instrumentationLibraryMetrics);
                }

                instrumentationLibraryMetrics.Metrics.Add(otlpMetric);
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
                    {
                        var sum = new OtlpMetrics.Sum();
                        sum.IsMonotonic = true;
                        sum.AggregationTemporality = temporality;

                        foreach (ref var metricPoint in metric.GetMetricPoints())
                        {
                            var dataPoint = new OtlpMetrics.NumberDataPoint
                            {
                                StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                                TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                            };

                            AddAttributes(metricPoint.Keys, metricPoint.Values, dataPoint.Attributes);

                            dataPoint.AsInt = metricPoint.LongValue;
                            sum.DataPoints.Add(dataPoint);
                        }

                        otlpMetric.Sum = sum;
                        break;
                    }

                case MetricType.DoubleSum:
                    {
                        var sum = new OtlpMetrics.Sum();
                        sum.IsMonotonic = true;
                        sum.AggregationTemporality = temporality;

                        foreach (ref var metricPoint in metric.GetMetricPoints())
                        {
                            var dataPoint = new OtlpMetrics.NumberDataPoint
                            {
                                StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                                TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                            };

                            AddAttributes(metricPoint.Keys, metricPoint.Values, dataPoint.Attributes);

                            dataPoint.AsDouble = metricPoint.DoubleValue;
                            sum.DataPoints.Add(dataPoint);
                        }

                        otlpMetric.Sum = sum;
                        break;
                    }

                case MetricType.LongGauge:
                    {
                        var gauge = new OtlpMetrics.Gauge();
                        foreach (ref var metricPoint in metric.GetMetricPoints())
                        {
                            var dataPoint = new OtlpMetrics.NumberDataPoint
                            {
                                StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                                TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                            };

                            AddAttributes(metricPoint.Keys, metricPoint.Values, dataPoint.Attributes);

                            dataPoint.AsInt = metricPoint.LongValue;
                            gauge.DataPoints.Add(dataPoint);
                        }

                        otlpMetric.Gauge = gauge;
                        break;
                    }

                case MetricType.DoubleGauge:
                    {
                        var gauge = new OtlpMetrics.Gauge();
                        foreach (ref var metricPoint in metric.GetMetricPoints())
                        {
                            var dataPoint = new OtlpMetrics.NumberDataPoint
                            {
                                StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                                TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                            };

                            AddAttributes(metricPoint.Keys, metricPoint.Values, dataPoint.Attributes);

                            dataPoint.AsDouble = metricPoint.DoubleValue;
                            gauge.DataPoints.Add(dataPoint);
                        }

                        otlpMetric.Gauge = gauge;
                        break;
                    }

                case MetricType.Histogram:
                    {
                        var histogram = new OtlpMetrics.Histogram();
                        histogram.AggregationTemporality = temporality;

                        foreach (ref var metricPoint in metric.GetMetricPoints())
                        {
                            var dataPoint = new OtlpMetrics.HistogramDataPoint
                            {
                                StartTimeUnixNano = (ulong)metricPoint.StartTime.ToUnixTimeNanoseconds(),
                                TimeUnixNano = (ulong)metricPoint.EndTime.ToUnixTimeNanoseconds(),
                            };

                            AddAttributes(metricPoint.Keys, metricPoint.Values, dataPoint.Attributes);
                            dataPoint.Count = (ulong)metricPoint.LongValue;
                            dataPoint.Sum = metricPoint.DoubleValue;

                            if (metricPoint.BucketCounts != null)
                            {
                                for (int i = 0; i < metricPoint.BucketCounts.Length; i++)
                                {
                                    dataPoint.BucketCounts.Add((ulong)metricPoint.BucketCounts[i]);
                                    if (i < metricPoint.BucketCounts.Length - 1)
                                    {
                                        dataPoint.ExplicitBounds.Add(metricPoint.ExplicitBounds[i]);
                                    }
                                }
                            }

                            histogram.DataPoints.Add(dataPoint);
                        }

                        otlpMetric.Histogram = histogram;
                        break;
                    }
            }

            return otlpMetric;
        }

        private static void AddAttributes(string[] keys, object[] values, RepeatedField<OtlpCommon.KeyValue> attributes)
        {
            if (keys != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    KeyValuePair<string, object> tag = new KeyValuePair<string, object>(keys[i], values[i]);
                    attributes.Add(tag.ToOtlpAttribute());
                }
            }
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
