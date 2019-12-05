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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Configuration;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Metrics.Implementation;
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
            List<Metric> metrics = new List<Metric>();
            var promExporter = new PrometheusExporter(promOptions);
            try
            {
                promExporter.Start();
                var label1 = new List<KeyValuePair<string, string>>();
                label1.Add(new KeyValuePair<string, string>("dim1", "value1"));
                metrics.Add(new Metric("ns", "metric1", "desc", label1, 100));
                metrics.Add(new Metric("ns", "metric1", "desc", label1, 100));
                metrics.Add(new Metric("ns", "metric1", "desc", label1, 100));

                promExporter.ExportAsync(metrics, CancellationToken.None);
            }
            finally
            {
                Task.Delay(10000).Wait();
                promExporter.Stop();
            }
        }

        [Fact]
        public void E2ETest1()
        {
            var promOptions = new PrometheusExporterOptions() { Url = "http://localhost:9184/metrics/" };
            var promExporter = new PrometheusExporter(promOptions);
            var simpleProcessor = new SimpleMetricProcessor(promExporter, TimeSpan.FromSeconds(5));
            var meter = MeterFactory.Create(simpleProcessor).GetMeter("library1") as MeterSDK;
            var testCounter = meter.CreateInt64Counter("testCounter");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));

            try
            {
                promExporter.Start();

                testCounter.Add(SpanContext.BlankLocal, 100, meter.GetLabelSet(labels1));
                testCounter.Add(SpanContext.BlankLocal, 10, meter.GetLabelSet(labels1));
                testCounter.Add(SpanContext.BlankLocal, 200, meter.GetLabelSet(labels2));
                testCounter.Add(SpanContext.BlankLocal, 10, meter.GetLabelSet(labels2));

                meter.Collect();
            }
            finally
            {
                Task.Delay(30000).Wait();
                promExporter.Stop();
            }
        }
    }
}
