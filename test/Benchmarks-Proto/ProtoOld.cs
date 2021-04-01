// <copyright file="ProtoOld.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;
using System.Linq;
using Google.Protobuf;
using Google.Protobuf.Collections;
using Opentelemetry4.Proto.Collector.Metrics.V1;
using Opentelemetry4.Proto.Common.V1;
using Opentelemetry4.Proto.Metrics.V1;

namespace ProtoBench
{
    public class ProtoOld
    {
        public static byte[] Encode(
            bool isDouble,
            (string name, object value)[] resources,
            (string name, string value)[] labels,
            int numLibs,
            int numMetrics,
            int numPoints)
        {
            DateTimeOffset dt = DateTimeOffset.UtcNow;

            var resmetric = new ResourceMetrics();

            for (var lib = 0; lib < numLibs; lib++)
            {
                var instMetric = new InstrumentationLibraryMetrics();
                instMetric.InstrumentationLibrary = new InstrumentationLibrary();
                instMetric.InstrumentationLibrary.Name = $"Library{lib}";
                instMetric.InstrumentationLibrary.Version = "1.0.0";

                for (int m = 0; m < numMetrics; m++)
                {
                    Metric metric = new Metric();

                    metric.MetricDescriptor = new MetricDescriptor()
                    {
                        Name = $"Metric_{m}",
                        Type = MetricDescriptor.Types.Type.Int64,
                    };

                    RepeatedField<StringKeyValue> stringLabels = new ();
                    foreach (var l in labels)
                    {
                        var kv = new StringKeyValue();
                        kv.Key = l.name;
                        kv.Value = l.value;
                        stringLabels.Add(kv);
                    }

                    if (isDouble)
                    {
                        for (int dp = 0; dp < numPoints; dp++)
                        {
                            var datapoint = new DoubleDataPoint();
                            datapoint.StartTimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.TimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.Labels.AddRange(stringLabels);
                            datapoint.Value = (double)(dp + 100.1);

                            metric.DoubleDataPoints.Add(datapoint);
                        }
                    }
                    else
                    {
                        for (int dp = 0; dp < numPoints; dp++)
                        {
                            var datapoint = new Int64DataPoint();
                            datapoint.StartTimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.TimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.Labels.AddRange(stringLabels);
                            datapoint.Value = (long)(dp + 100);

                            metric.Int64DataPoints.Add(datapoint);
                        }
                    }

                    instMetric.Metrics.Add(metric);
                }

                resmetric.InstrumentationLibraryMetrics.Add(instMetric);
            }

            resmetric.Resource = new Opentelemetry4.Proto.Resource.V1.Resource();
            resmetric.Resource.DroppedAttributesCount = 0;
            var attribs = resmetric.Resource.Attributes;
            foreach (var resource in resources)
            {
                var kv = new KeyValue();
                kv.Key = resource.name;
                kv.Value = new AnyValue();
                if (resource.value is string s)
                {
                    kv.Value.StringValue = s;
                }
                else if (resource.value is long l)
                {
                    kv.Value.IntValue = l;
                }

                attribs.Add(kv);
            }

            var request = new ExportMetricsServiceRequest();
            request.ResourceMetrics.Add(resmetric);
            var bytes = request.ToByteArray();
            return bytes;
        }

        public static byte[] EncodeSummary(
            (string name, object value)[] resources,
            (string name, string value)[] labels,
            int numLibs,
            int numMetrics,
            int numTimeseries,
            int numQV)
        {
            DateTimeOffset dt = DateTimeOffset.UtcNow;

            var resmetric = new ResourceMetrics();

            for (var lib = 0; lib < numLibs; lib++)
            {
                var instMetric = new InstrumentationLibraryMetrics();
                instMetric.InstrumentationLibrary = new InstrumentationLibrary();
                instMetric.InstrumentationLibrary.Name = $"Library{lib}";
                instMetric.InstrumentationLibrary.Version = "1.0.0";

                for (int m = 0; m < numMetrics; m++)
                {
                    Metric metric = new Metric();

                    metric.MetricDescriptor = new MetricDescriptor()
                    {
                        Name = $"Metric_{m}",
                        Type = MetricDescriptor.Types.Type.Int64,
                    };

                    RepeatedField<StringKeyValue> stringLabels = new ();
                    foreach (var l in labels)
                    {
                        var kv = new StringKeyValue();
                        kv.Key = l.name;
                        kv.Value = l.value;
                        stringLabels.Add(kv);
                    }

                    for (int tsx = 0; tsx < numTimeseries; tsx++)
                    {
                        var datapoint = new SummaryDataPoint();
                        datapoint.StartTimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                        datapoint.TimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                        datapoint.Labels.AddRange(stringLabels);

                        for (int qv = 0; qv < numQV; qv++)
                        {
                            var v = new SummaryDataPoint.Types.ValueAtPercentile();
                            v.Percentile = qv * (1 / numQV);
                            v.Value = 1.0;

                            datapoint.PercentileValues.Add(v);
                        }

                        metric.SummaryDataPoints.Add(datapoint);
                    }

                    instMetric.Metrics.Add(metric);
                }

                resmetric.InstrumentationLibraryMetrics.Add(instMetric);
            }

            resmetric.Resource = new Opentelemetry4.Proto.Resource.V1.Resource();
            resmetric.Resource.DroppedAttributesCount = 0;
            var attribs = resmetric.Resource.Attributes;
            foreach (var resource in resources)
            {
                var kv = new KeyValue();
                kv.Key = resource.name;
                kv.Value = new AnyValue();
                if (resource.value is string s)
                {
                    kv.Value.StringValue = s;
                }
                else if (resource.value is long l)
                {
                    kv.Value.IntValue = l;
                }

                attribs.Add(kv);
            }

            var request = new ExportMetricsServiceRequest();
            request.ResourceMetrics.Add(resmetric);
            var bytes = request.ToByteArray();
            return bytes;
        }

        public static byte[] EncodeHistogram(
            (string name, object value)[] resources,
            (string name, string value)[] labels,
            int numLibs,
            int numMetrics,
            int numTimeseries,
            int numQV,
            int numExemplars)
        {
            DateTimeOffset dt = DateTimeOffset.UtcNow;

            var resmetric = new ResourceMetrics();

            for (var lib = 0; lib < numLibs; lib++)
            {
                var instMetric = new InstrumentationLibraryMetrics();
                instMetric.InstrumentationLibrary = new InstrumentationLibrary();
                instMetric.InstrumentationLibrary.Name = $"Library{lib}";
                instMetric.InstrumentationLibrary.Version = "1.0.0";

                for (int m = 0; m < numMetrics; m++)
                {
                    Metric metric = new Metric();

                    metric.MetricDescriptor = new MetricDescriptor()
                    {
                        Name = $"Metric_{m}",
                        Type = MetricDescriptor.Types.Type.Int64,
                    };

                    RepeatedField<StringKeyValue> stringLabels = new ();
                    foreach (var l in labels)
                    {
                        var kv = new StringKeyValue();
                        kv.Key = l.name;
                        kv.Value = l.value;
                        stringLabels.Add(kv);
                    }

                    for (int tsx = 0; tsx < numTimeseries; tsx++)
                    {
                        var datapoint = new HistogramDataPoint();
                        datapoint.StartTimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                        datapoint.TimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                        datapoint.Labels.AddRange(stringLabels);

                        for (int qv = 0; qv < numQV; qv++)
                        {
                            datapoint.ExplicitBounds.Add(qv * (1 / numQV));
                            datapoint.Count++;
                            datapoint.Sum += qv;

                            for (int ex = 0; ex < numExemplars; ex++)
                            {
                                var buck = new HistogramDataPoint.Types.Bucket();
                                buck.Exemplar = new HistogramDataPoint.Types.Bucket.Types.Exemplar();
                                buck.Count++;
                                buck.Exemplar.Value = qv;
                                datapoint.Buckets.Add(buck);
                            }
                        }

                        metric.HistogramDataPoints.Add(datapoint);
                    }

                    instMetric.Metrics.Add(metric);
                }

                resmetric.InstrumentationLibraryMetrics.Add(instMetric);
            }

            resmetric.Resource = new Opentelemetry4.Proto.Resource.V1.Resource();
            resmetric.Resource.DroppedAttributesCount = 0;
            var attribs = resmetric.Resource.Attributes;
            foreach (var resource in resources)
            {
                var kv = new KeyValue();
                kv.Key = resource.name;
                kv.Value = new AnyValue();
                if (resource.value is string s)
                {
                    kv.Value.StringValue = s;
                }
                else if (resource.value is long l)
                {
                    kv.Value.IntValue = l;
                }

                attribs.Add(kv);
            }

            var request = new ExportMetricsServiceRequest();
            request.ResourceMetrics.Add(resmetric);
            var bytes = request.ToByteArray();
            return bytes;
        }

        public static List<string> Decode(byte[] bytes)
        {
            var parser = new Google.Protobuf.MessageParser<ExportMetricsServiceRequest>(() => new ExportMetricsServiceRequest());
            var request = parser.ParseFrom(bytes);

            List<string> extracts = new ();

            foreach (var resmetric in request.ResourceMetrics)
            {
                foreach (var attr in resmetric.Resource.Attributes)
                {
                    switch (attr.Value.ValueCase)
                    {
                        case AnyValue.ValueOneofCase.StringValue:
                            extracts.Add($"{attr.Key}={attr.Value.StringValue}");
                            break;

                        case AnyValue.ValueOneofCase.IntValue:
                            extracts.Add($"{attr.Key}={attr.Value.IntValue}");
                            break;

                        case AnyValue.ValueOneofCase.DoubleValue:
                            extracts.Add($"{attr.Key}={attr.Value.DoubleValue}");
                            break;

                        default:
                            extracts.Add($"{attr.Key}={attr.Value}");
                            break;
                    }
                }

                foreach (var meter in resmetric.InstrumentationLibraryMetrics)
                {
                    extracts.Add($"{meter.InstrumentationLibrary.Name}/{meter.InstrumentationLibrary.Version}");

                    foreach (var metric in meter.Metrics)
                    {
                        extracts.Add(metric.MetricDescriptor.Name);

                        if (metric.DoubleDataPoints.Count > 0)
                        {
                            double sumd = 0.0;
                            foreach (var dp in metric.DoubleDataPoints)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                sumd += dp.Value;
                            }

                            extracts.Add($"sum:{sumd}");
                        }

                        if (metric.Int64DataPoints.Count > 0)
                        {
                            long suml = 0;
                            foreach (var dp in metric.Int64DataPoints)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                suml += dp.Value;
                            }

                            extracts.Add($"sum:{suml}");
                        }

                        if (metric.SummaryDataPoints.Count > 0)
                        {
                            foreach (var dp in metric.SummaryDataPoints)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                extracts.Add($"{dp.Count}/{dp.Sum}");

                                extracts.Add(string.Join(",", dp.PercentileValues.Select(pv => $"{pv.Percentile}:{pv.Value}")));
                            }
                        }

                        if (metric.HistogramDataPoints.Count > 0)
                        {
                            double sumd = 0.0;

                            foreach (var dp in metric.HistogramDataPoints)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                extracts.Add(string.Join(",", dp.Buckets.Select(k => $"{k.Count}")));

                                extracts.Add(string.Join(",", dp.ExplicitBounds.Select(k => $"{k}")));

                                foreach (var buck in dp.Buckets)
                                {
                                    extracts.Add(string.Join(",", buck.Exemplar.Attachments.Select(lbl => $"{lbl.Key}={lbl.Value}")));
                                    sumd += buck.Exemplar.Value;
                                }
                            }

                            extracts.Add($"sum:{sumd}");
                        }
                    }
                }
            }

            return extracts;
        }
    }
}
