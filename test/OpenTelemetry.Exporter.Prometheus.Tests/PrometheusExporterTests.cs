// <copyright file="PrometheusExporterTests.cs" company="OpenTelemetry Authors">
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
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics.Configuration;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Trace;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Exporter.Prometheus.Tests
{
    public class PrometheusExporterTests
    {
        private readonly ITestOutputHelper output;
        private const int metricPushIntervalMsec = 100;
        private const int waitDuration = metricPushIntervalMsec + 10;
        private const int numOperationBatches = 10;

        public PrometheusExporterTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public async Task E2ETestMetricsHttpServerAsync()
        {
            var promOptions = new PrometheusExporterOptions() { Url = "http://localhost:9184/metrics/" };
            var promExporter = new PrometheusExporter(promOptions);
            var simpleProcessor = new UngroupedBatcher();

            var metricsHttpServer = new PrometheusExporterMetricsHttpServer(promExporter);
            try
            {
                metricsHttpServer.Start();
                CollectMetrics(simpleProcessor, promExporter);
            }
            finally
            {
                await Task.Delay(waitDuration);

                var client = new HttpClient();
                var response = await client.GetAsync("http://localhost:9184/metrics/");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var responseText = response.Content.ReadAsStringAsync().Result;
                this.output.WriteLine($"Response from metrics API is\n{responseText}");
                ValidateResponse(responseText);
                metricsHttpServer.Stop();
            }
        }

        [Fact]
        public async Task E2ETestMiddleware()
        {
            var promOptions = new PrometheusExporterOptions() { Url = "/metrics" };
            var promExporter = new PrometheusExporter(promOptions);
            var simpleProcessor = new UngroupedBatcher();

            var builder = new WebHostBuilder()
                .Configure(app =>
                {
                    app.UsePrometheus();
                })
                .ConfigureServices(services =>
                {
                    services.AddSingleton(promOptions);
                    services.AddSingleton(promExporter); //Temporary till we figure out metrics configuration
                });

            var server = new TestServer(builder);
            var client = server.CreateClient();

            try
            {
                CollectMetrics(simpleProcessor, promExporter);
            }
            finally
            {

                var response = await client.GetAsync("/foo");
                Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

                await Task.Delay(waitDuration);
                response = await client.GetAsync("/metrics");
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                var responseText = response.Content.ReadAsStringAsync().Result;
                this.output.WriteLine($"Response from metrics API is\n{responseText}");
                ValidateResponse(responseText);
            }
        }

        private static void CollectMetrics(UngroupedBatcher simpleProcessor, MetricExporter exporter)
        {
            var meter = MeterFactory.Create(mb =>
            {
                mb.SetMetricProcessor(simpleProcessor);
                mb.SetMetricExporter(exporter);
                mb.SetMetricPushInterval(TimeSpan.FromMilliseconds(metricPushIntervalMsec));
            }).GetMeter("library1");

            var testCounter = meter.CreateInt64Counter("testCounter");
            var testMeasure = meter.CreateInt64Measure("testMeasure");

            var labels1 = new List<KeyValuePair<string, string>>();
            labels1.Add(new KeyValuePair<string, string>("dim1", "value1"));
            labels1.Add(new KeyValuePair<string, string>("dim2", "value1"));

            var labels2 = new List<KeyValuePair<string, string>>();
            labels2.Add(new KeyValuePair<string, string>("dim1", "value2"));
            labels2.Add(new KeyValuePair<string, string>("dim2", "value2"));


            var defaultContext = default(SpanContext);

            for (int i = 0; i < numOperationBatches; i++)
            {
                testCounter.Add(defaultContext, 100, meter.GetLabelSet(labels1));
                testCounter.Add(defaultContext, 10, meter.GetLabelSet(labels1));
                testCounter.Add(defaultContext, 200, meter.GetLabelSet(labels2));
                testCounter.Add(defaultContext, 10, meter.GetLabelSet(labels2));

                testMeasure.Record(defaultContext, 10, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 100, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 5, meter.GetLabelSet(labels2));
                testMeasure.Record(defaultContext, 500, meter.GetLabelSet(labels2));
            }
        }

        private static void ValidateResponse(string responseText)
        {
            var prometheusLabels = new []
            {
                "dim1=\"value1\",dim2=\"value1\"",
                "dim1=\"value2\",dim2=\"value2\"",
            };

            var counterCases = new []
            {
                new { labels = prometheusLabels[0], adds = new [] { 100, 10} },
                new { labels = prometheusLabels[1], adds = new [] { 200, 10} },
            };

            var measureCases = new []
            {
                new { labels = prometheusLabels[0], measures = new []{ 10, 100 } },
                new { labels = prometheusLabels[1], measures = new []{ 5, 500 } },
            };

            // Validate counters.
            var responseLines = responseText.Split('\n');
            Assert.Single(responseLines, l  => l == "# TYPE testCounter counter");
            foreach (var counterCase in counterCases)
            {
                var counter = numOperationBatches * counterCase.adds.Sum();
                var counterLine = $"testCounter{{{counterCase.labels}}} {counter.ToString(CultureInfo.InvariantCulture)}";
                Assert.Single(responseLines, l  => l == counterLine);
            }

            // Validate measures.
            Assert.Single(responseLines, l => l == "# TYPE testMeasure summary");
            foreach (var measureCase in measureCases)
            {
                var min = measureCase.measures.Min();
                var max = measureCase.measures.Max();
                var sum = numOperationBatches * measureCase.measures.Sum();
                var count = numOperationBatches * measureCase.measures.Length;

                var minLine = $"testMeasure{{{measureCase.labels},quantile=\"0\"}} {min.ToString(CultureInfo.InvariantCulture)}";
                Assert.Single(responseLines, l => l == minLine);

                var maxLine = $"testMeasure{{{measureCase.labels},quantile=\"1\"}} {max.ToString(CultureInfo.InvariantCulture)}";
                Assert.Single(responseLines, l => l == maxLine);

                var sumLine = $"testMeasure_sum{{{measureCase.labels}}} {sum.ToString(CultureInfo.InvariantCulture)}";
                Assert.Single(responseLines, l => l == sumLine);

                var countLine = $"testMeasure_count{{{measureCase.labels}}} {count.ToString(CultureInfo.InvariantCulture)}";
                Assert.Single(responseLines, l => l == countLine);
            }

            // TODO: Validate order of the lines.

            // If in future, there is a official .NET Prometheus Client library, and OT Exporter
            // chooses to take a dependency on it.
        }
    }
}
