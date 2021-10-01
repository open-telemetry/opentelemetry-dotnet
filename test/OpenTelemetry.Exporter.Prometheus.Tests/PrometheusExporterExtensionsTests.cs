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
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests
{
    public sealed class PrometheusExporterExtensionsTests
    {
        [Fact]
        public void WriteMetricsCollectionTest()
        {
            var tags = new KeyValuePair<string, object>[]
            {
                new KeyValuePair<string, object>("key1", "value1"),
                new KeyValuePair<string, object>("key2", "value2"),
            };

            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .AddReader(new BaseExportingMetricReader(new TestExporter<Metric>(RunTest)))
                .Build();

            using var meter = new Meter("TestMeter", "0.0.1");

            var counter = meter.CreateCounter<int>("counter");

            counter.Add(100, tags);

            var testCompleted = false;

            // Invokes the TestExporter which will invoke RunTest
            provider.ForceFlush(3000);

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
                    "# TYPE counter counter\ncounter{key1=\"value1\",key2=\"value2\"} 100 1633041000000\n",
                    Encoding.UTF8.GetString(ms.ToArray()));

                testCompleted = true;
            }
        }
    }
}
