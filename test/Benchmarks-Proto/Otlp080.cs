// <copyright file="Otlp080.cs" company="OpenTelemetry Authors">
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
using Opentelemetry080.Proto.Collector.Metrics.V1;
using Opentelemetry080.Proto.Common.V1;
using Opentelemetry080.Proto.Metrics.V1;
using Opentelemetry080.Proto.Resource.V1;

namespace ProtoBench
{
    public class Otlp080
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

            for (int lib = 0; lib < numLibs; lib++)
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

                    var gauge = new Gauge();
                    metric.Gauge = gauge;

                    if (isDouble)
                    {
                        for (int dp = 0; dp < numPoints; dp++)
                        {
                            var datapoint = new NumberDataPoint();

                            datapoint.StartTimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.TimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.Labels.AddRange(stringLabels);
                            datapoint.AsDouble = (double)(dp + 100.1);

                            gauge.DataPoints.Add(datapoint);
                        }
                    }
                    else
                    {
                        for (int dp = 0; dp < numPoints; dp++)
                        {
                            var datapoint = new NumberDataPoint();

                            datapoint.StartTimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.TimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                            datapoint.Labels.AddRange(stringLabels);
                            datapoint.AsInt = (long)(dp + 100);

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

            for (int lib = 0; lib < numLibs; lib++)
            {
                var instMetric = new InstrumentationLibraryMetrics();

                instMetric.InstrumentationLibrary = new InstrumentationLibrary();
                instMetric.InstrumentationLibrary.Name = $"Library{lib}";
                instMetric.InstrumentationLibrary.Version = "1.0.0";

                for (int m = 0; m < numMetrics; m++)
                {
                    Metric metric = new Metric();

                    metric.Name = $"Metric_{m}";
                    metric.Summary = new Summary();

                    var stringLabels = new RepeatedField<StringKeyValue>();
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
                        datapoint.Count = 1;
                        datapoint.Sum = tsx + 100.1;

                        for (int qv = 0; qv < numQV; qv++)
                        {
                            var qvalues = new SummaryDataPoint.Types.ValueAtQuantile();
                            qvalues.Quantile = qv * (1 / numQV);
                            qvalues.Value = 1.0;

                            datapoint.QuantileValues.Add(qvalues);
                        }

                        metric.Summary.DataPoints.Add(datapoint);
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

            var instMetrics = new List<InstrumentationLibraryMetrics>();

            for (int lib = 0; lib < numLibs; lib++)
            {
                var instMetric = new InstrumentationLibraryMetrics();

                instMetric.InstrumentationLibrary = new InstrumentationLibrary();
                instMetric.InstrumentationLibrary.Name = $"Library{lib}";
                instMetric.InstrumentationLibrary.Version = "1.0.0";

                for (int m = 0; m < numMetrics; m++)
                {
                    Metric metric = new Metric();
                    metric.Name = $"Metric_{m}";
                    metric.Histogram = new Histogram();

                    var stringLabels = new RepeatedField<StringKeyValue>();
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
                            datapoint.Count++;
                            datapoint.Sum += qv;
                            datapoint.BucketCounts.Add(1);
                            datapoint.ExplicitBounds.Add(qv * (1 / numQV));

                            for (int ex = 0; ex < numExemplars; ex++)
                            {
                                var exp = new Exemplar();
                                exp.TimeUnixNano = (ulong)dt.ToUnixTimeMilliseconds() * 100000;
                                exp.AsInt = ex;
                                datapoint.Exemplars.Add(exp);
                            }
                        }

                        metric.Histogram.DataPoints.Add(datapoint);
                    }

                    instMetric.Metrics.Add(metric);
                }

                instMetrics.Add(instMetric);
            }

            var resmetric = new ResourceMetrics();
            resmetric.InstrumentationLibraryMetrics.AddRange(instMetrics);

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

                        RepeatedField<NumberDataPoint> gaugedps = null;
                        RepeatedField<SummaryDataPoint> sumdps = null;
                        RepeatedField<HistogramDataPoint> histdps = null;

                        switch (metric.DataCase)
                        {
                            case Metric.DataOneofCase.Gauge:
                                gaugedps = metric.Gauge.DataPoints;
                                break;

                            case Metric.DataOneofCase.Summary:
                                sumdps = metric.Summary.DataPoints;
                                break;

                            case Metric.DataOneofCase.Histogram:
                                histdps = metric.Histogram.DataPoints;
                                break;
                        }

                        if (gaugedps is not null)
                        {
                            long suml = 0;
                            double sumd = 0;

                            foreach (var dp in gaugedps)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                switch (dp.ValueCase)
                                {
                                    case NumberDataPoint.ValueOneofCase.AsInt:
                                        suml += dp.AsInt;
                                        break;

                                    case NumberDataPoint.ValueOneofCase.AsDouble:
                                        sumd += dp.AsDouble;
                                        break;
                                }
                            }

                            extracts.Add($"sum:{suml}/{sumd}");
                        }

                        if (sumdps is not null)
                        {
                            foreach (var dp in sumdps)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                extracts.Add($"{dp.Count}/{dp.Sum}");

                                extracts.Add(string.Join(",", dp.QuantileValues.Select(qv => $"{qv.Quantile}:{qv.Value}")));
                            }
                        }

                        if (histdps is not null)
                        {
                            foreach (var dp in histdps)
                            {
                                extracts.Add(string.Join(",", dp.Labels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                extracts.Add(string.Join(",", dp.BucketCounts.Select(k => $"{k}")));

                                extracts.Add(string.Join(",", dp.ExplicitBounds.Select(k => $"{k}")));

                                long suml = 0;
                                double sumd = 0.0;

                                foreach (var xp in dp.Exemplars)
                                {
                                    extracts.Add(string.Join(",", xp.FilteredLabels.Select(lbl => $"{lbl.Key}={lbl.Value}")));

                                    switch (xp.ValueCase)
                                    {
                                        case Exemplar.ValueOneofCase.AsInt:
                                            suml += xp.AsInt;
                                            break;

                                        case Exemplar.ValueOneofCase.AsDouble:
                                            sumd += xp.AsDouble;
                                            break;
                                    }
                                }

                                extracts.Add($"sum:{suml}/{sumd}");
                            }
                        }
                    }
                }
            }

            return extracts;
        }
    }
}
