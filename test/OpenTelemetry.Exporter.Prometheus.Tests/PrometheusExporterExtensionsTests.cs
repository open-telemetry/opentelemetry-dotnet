// <copyright file="PrometheusExporterExtensionsTests.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.IO;
using System.Reflection;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests
{
    public sealed class PrometheusExporterExtensionsTests
    {
        private const string MeterName = "PrometheusExporterExtensionsTests.WriteMetricsCollectionTest.Meter";

        [Theory]
        [InlineData(nameof(WriteLongSum))]
        [InlineData(nameof(WriteDoubleSum))]
        [InlineData(nameof(WriteLongGauge))]
        [InlineData(nameof(WriteDoubleGauge))]
        [InlineData(nameof(WriteHistogram))]
        public void WriteMetricsCollectionTest(string writeActionMethodName)
        {
            using var meter = new Meter(MeterName, "0.0.1");

            MethodInfo writeAction = typeof(PrometheusExporterExtensionsTests).GetMethod(writeActionMethodName, BindingFlags.NonPublic | BindingFlags.Static);
            if (writeAction == null)
            {
                throw new InvalidOperationException($"Write action {writeActionMethodName} could not be found reflectively.");
            }

            var tags = new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>("key1", "value1"),
                new KeyValuePair<string, object>("key2", "value2"),
            };

            string tagsExpected = "{key1=\"value1\",key2=\"value2\"}";

            string expected = null;
            var testCompleted = false;

            using (var provider = Sdk.CreateMeterProviderBuilder()
                .AddSource(MeterName)
                .AddReader(new BaseExportingMetricReader(new TestExporter<Metric>(RunTest)))
                .Build())
            {
                expected = (string)writeAction.Invoke(null, new object[] { meter, tags, tagsExpected });
            }

            Assert.True(testCompleted);

            void RunTest(Batch<Metric> metrics)
            {
                using PrometheusExporter prometheusExporter = new PrometheusExporter(new PrometheusExporterOptions());

                prometheusExporter.Metrics = metrics;

                using MemoryStream ms = new MemoryStream();
                using (StreamWriter writer = new StreamWriter(ms))
                {
                    PrometheusExporterExtensions.WriteMetricsCollection(prometheusExporter, writer, () => new DateTimeOffset(2021, 9, 30, 22, 30, 0, TimeSpan.Zero)).GetAwaiter().GetResult();
                }

                Assert.Equal(
                    expected,
                    Encoding.UTF8.GetString(ms.ToArray()));

                testCompleted = true;
            }
        }

        private static string WriteLongSum(Meter meter, KeyValuePair<string, object>[] tags, string tagsExpected)
        {
            var counter = meter.CreateCounter<int>("counter_int", description: "Prometheus help text goes here \n escaping.");
            counter.Add(100, tags);

            return $"# HELP counter_intPrometheus help text goes here \\n escaping.\n# TYPE counter_int counter\ncounter_int{tagsExpected} 100 1633041000000\n";
        }

        private static string WriteDoubleSum(Meter meter, KeyValuePair<string, object>[] tags, string tagsExpected)
        {
            var counter = meter.CreateCounter<double>("counter_double");
            counter.Add(100.18D, tags);
            counter.Add(0.99D, tags);

            return $"# TYPE counter_double counter\ncounter_double{tagsExpected} 101.17 1633041000000\n";
        }

        private static string WriteLongGauge(Meter meter, KeyValuePair<string, object>[] tags, string tagsExpected)
        {
            var gauge = meter.CreateObservableGauge(
                "gauge_long",
                () => new Measurement<long>[] { new Measurement<long>(18, tags) });

            return $"# TYPE gauge_long gauge\ngauge_long{tagsExpected} 18 1633041000000\n";
        }

        private static string WriteDoubleGauge(Meter meter, KeyValuePair<string, object>[] tags, string tagsExpected)
        {
            var value = 0.18F;

            var gauge = meter.CreateObservableGauge(
                "gauge_double",
                () => new Measurement<float>[] { new Measurement<float>(99F, tags), new Measurement<float>(value, tags) });

            return $"# TYPE gauge_double gauge\ngauge_double{tagsExpected} {(double)value} 1633041000000\n";
        }

        private static string WriteHistogram(Meter meter, KeyValuePair<string, object>[] tags, string tagsExpected)
        {
            var histogram = meter.CreateHistogram<long>("histogram_name");
            histogram.Record(100, tags);
            histogram.Record(18, tags);

            return "# TYPE histogram_name histogram\nhistogram_name_sum{key1=\"value1\",key2=\"value2\"} 118 1633041000000\n"
               + "histogram_name_count{key1=\"value1\",key2=\"value2\"} 2 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"0\"} 0 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"5\"} 0 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"10\"} 0 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"25\"} 1 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"50\"} 1 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"75\"} 1 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"100\"} 2 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"250\"} 2 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"500\"} 2 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"1000\"} 2 1633041000000\n"
               + "histogram_name_bucket{key1=\"value1\",key2=\"value2\",le=\"+Inf\"} 2 1633041000000\n";
        }
    }
}
