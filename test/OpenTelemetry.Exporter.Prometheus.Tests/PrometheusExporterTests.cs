﻿// <copyright file="PrometheusExporterTests.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Configuration;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests
{
    public class PrometheusExporterTests
    {
        [Fact]
        public void PrometheusExporterTest1()
        {
            var promOptions = new PrometheusExporterOptions() { Url = "http://localhost:9184/metrics/" };
            List<Metric<long>> metrics = new List<Metric<long>>();
            var promExporter = new PrometheusExporter(promOptions);
            var metricsHttpServer = new PrometheusExporterMetricsHttpServer(promExporter);

            try
            {
                metricsHttpServer.Start();
                var label1 = new List<KeyValuePair<string, string>>();
                label1.Add(new KeyValuePair<string, string>("dim1", "value1"));
                metrics.Add(new Metric<long>("ns", "metric1", "desc", label1, AggregationType.LongSum));
                metrics.Add(new Metric<long>("ns", "metric1", "desc", label1, AggregationType.LongSum));
                metrics.Add(new Metric<long>("ns", "metric1", "desc", label1, AggregationType.LongSum));

                promExporter.ExportAsync(metrics, CancellationToken.None);
            }
            finally
            {
                // Change delay to higher value to manually check Promtheus.
                // These tests are just to temporarily validate export to prometheus.
                Task.Delay(10).Wait();
                metricsHttpServer.Stop();
            }
        }

        [Fact]
        public void E2ETest1()
        {
            var promOptions = new PrometheusExporterOptions() { Url = "http://localhost:9184/metrics/" };
            var promExporter = new PrometheusExporter(promOptions);
            var simpleProcessor = new UngroupedBatcher(promExporter);
            var meter = MeterFactory.Create(simpleProcessor).GetMeter("library1") as MeterSdk;
            var testCounter = meter.CreateInt64Counter("testCounter");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            var metricsHttpServer = new PrometheusExporterMetricsHttpServer(promExporter);
            try
            {
                metricsHttpServer.Start();

                var defaultContext = default(SpanContext);

                for (int i = 0; i < 1000; i++)
                {
                    testCounter.Add(defaultContext, 100, meter.GetLabelSet(labels1));
                    testCounter.Add(defaultContext, 10, meter.GetLabelSet(labels1));
                    testCounter.Add(defaultContext, 200, meter.GetLabelSet(labels2));
                    testCounter.Add(defaultContext, 10, meter.GetLabelSet(labels2));

                    if (i % 10 == 0)
                    {
                        meter.Collect();
                    }

                    // Change delay to higher value to manually check Promtheus.
                    // These tests are just to temporarily validate export to prometheus.
                    // Task.Delay(100).Wait();
                }
            }
            finally
            {
                Task.Delay(100).Wait();
                metricsHttpServer.Stop();
            }
        }
    }
}
