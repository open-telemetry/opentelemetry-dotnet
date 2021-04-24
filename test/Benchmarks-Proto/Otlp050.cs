// <copyright file="Otlp050.cs" company="OpenTelemetry Authors">
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
using Opentelemetry050.Proto.Collector.Metrics.V1;
using Opentelemetry050.Proto.Common.V1;
using Opentelemetry050.Proto.Metrics.V1;
using Opentelemetry050.Proto.Resource.V1;

namespace ProtoBench
{
    public class Otlp050
    {
        public static byte[] EncodeGauge(
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

                    metric.Name = $"Metric_{m}";

                    var stringLabels = new RepeatedField<StringKeyValue>();
                    foreach (var l in labels)
                    {
                        var kv = new StringKeyValue();
                        kv.Key = l.name;
                        kv.Value = l.value;
                        stringLabels.Add(kv);
                    }

                    if (isDouble)
                    {
                        var gauge = new DoubleGauge();
                        metric.DoubleGauge = gauge;

                        for (int dp = 0; dp < numPoints; dp++)
                        {
                            var datapoint = new DoubleDataPoint();
                            datapoint.StartTimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.TimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.Labels.AddRange(stringLabels);
                            datapoint.Value = (double)(dp + 100.1);

                            gauge.DataPoints.Add(datapoint);
                        }
                    }
                    else
                    {
                        var gauge = new IntGauge();
                        metric.IntGauge = gauge;

                        for (int dp = 0; dp < numPoints; dp++)
                        {
                            var datapoint = new IntDataPoint();
                            datapoint.StartTimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.TimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.Labels.AddRange(stringLabels);
                            datapoint.Value = (long)(dp + 100);

                            gauge.DataPoints.Add(datapoint);
                        }
                    }

                    instMetric.Metrics.Add(metric);
                }

                resmetric.InstrumentationLibraryMetrics.Add(instMetric);
            }

            resmetric.Resource = new Resource();
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

                    metric.Name = $"Metric_{m}";

                    var stringLabels = new RepeatedField<StringKeyValue>();
                    foreach (var l in labels)
                    {
                        var kv = new StringKeyValue();
                        kv.Key = l.name;
                        kv.Value = l.value;
                        stringLabels.Add(kv);
                    }

                    var summary = new DoubleSum();
                    metric.DoubleSum = summary;

                    for (int tsx = 0; tsx < numTimeseries; tsx++)
                    {
                        var datapoint = new DoubleDataPoint();
                        datapoint.StartTimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                        datapoint.TimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                        datapoint.Labels.AddRange(stringLabels);
                        datapoint.Value = tsx;

                        summary.DataPoints.Add(datapoint);
                    }

                    instMetric.Metrics.Add(metric);
                }

                resmetric.InstrumentationLibraryMetrics.Add(instMetric);
            }

            resmetric.Resource = new Resource();
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

                    metric.Name = $"Metric_{m}";

                    var stringLabels = new RepeatedField<StringKeyValue>();
                    foreach (var l in labels)
                    {
                        var kv = new StringKeyValue();
                        kv.Key = l.name;
                        kv.Value = l.value;
                        stringLabels.Add(kv);
                    }

                    var histogram = new DoubleHistogram();
                    metric.DoubleHistogram = histogram;

                    for (int tsx = 0; tsx < numTimeseries; tsx++)
                    {
                        var datapoint = new DoubleHistogramDataPoint();
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
                                var exemplar = new DoubleExemplar();
                                exemplar.Value = ex;
                                datapoint.Exemplars.Add(exemplar);
                            }
                        }

                        histogram.DataPoints.Add(datapoint);
                    }

                    instMetric.Metrics.Add(metric);
                }

                resmetric.InstrumentationLibraryMetrics.Add(instMetric);
            }

            resmetric.Resource = new Resource();
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

            var extracts = new List<string>();

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
                        extracts.Add(metric.Name);

                        if (metric.DoubleGauge is not null)
                        {
                            double sumd = 0.0;
                            foreach (var dp in metric.DoubleGauge.DataPoints)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                sumd += dp.Value;
                            }

                            extracts.Add($"sum:{sumd}");
                        }

                        if (metric.IntGauge is not null)
                        {
                            long suml = 0;
                            foreach (var dp in metric.IntGauge.DataPoints)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                suml += dp.Value;
                            }

                            extracts.Add($"sum:{suml}");
                        }

                        if (metric.DoubleSum is not null)
                        {
                            foreach (var dp in metric.DoubleSum.DataPoints)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                extracts.Add($"{dp.Value}");

                                extracts.Add(string.Join(",", dp.Exemplars.Select(ex => $"{ex.Value}")));
                            }
                        }

                        if (metric.DoubleHistogram is not null)
                        {
                            foreach (var dp in metric.DoubleHistogram.DataPoints)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                extracts.Add($"count:{dp.Count}");
                                extracts.Add($"sum:{dp.Sum}");

                                extracts.Add(string.Join(",", dp.ExplicitBounds.Select(k => $"{k}")));

                                extracts.Add(string.Join(",", dp.Exemplars.Select(ex => $"{ex.Value}")));
                            }
                        }
                    }
                }
            }

            return extracts;
        }
    }
}
