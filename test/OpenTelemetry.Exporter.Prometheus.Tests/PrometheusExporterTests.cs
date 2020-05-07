// <copyright file="PrometheusExporterTests.cs" company="OpenTelemetry Authors">
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
        private const int waitDuration = metricPushIntervalMsec + 100;


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
                this.output.WriteLine($"Respone from metrics API is \n {responseText}");
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
                this.output.WriteLine($"Respone from metrics API is \n {responseText}");
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

            for (int i = 0; i < 10; i++)
            {
                testCounter.Add(defaultContext, 100, meter.GetLabelSet(labels1));
                testCounter.Add(defaultContext, 10, meter.GetLabelSet(labels1));
                testCounter.Add(defaultContext, 200, meter.GetLabelSet(labels2));
                testCounter.Add(defaultContext, 10, meter.GetLabelSet(labels2));

                testMeasure.Record(defaultContext, 10, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 100, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 5, meter.GetLabelSet(labels1));
                testMeasure.Record(defaultContext, 500, meter.GetLabelSet(labels1));
            }
        }

        private void ValidateResponse(string responseText)
        {
            // Validate counters.
            Assert.Contains("TYPE testCounter counter", responseText);
            Assert.Contains("testCounter{dim1=\"value1\",dim2=\"value1\"}", responseText);
            Assert.Contains("testCounter{dim1=\"value2\",dim2=\"value2\"}", responseText);

            // Validate measure.
            Assert.Contains("# TYPE testMeasure summary", responseText);
            // sum is 6150 = 10 * (10+100+5+500)
            Assert.Contains("testMeasure_sum{dim1=\"value1\"} 6150", responseText);
            // count is 10 * 4
            Assert.Contains("testMeasure_count{dim1=\"value1\"} 40", responseText);
            // Min is 5
            Assert.Contains("testMeasure{dim1=\"value1\",quantile=\"0\"} 5", responseText);
            // Max is 500
            Assert.Contains("testMeasure{dim1=\"value1\",quantile=\"1\"} 500", responseText);

            // TODO: Validate that # TYPE occurs only once per metric.
            // Also validate that for every metric+dimension, there is only one row in the response.
            // Though the above are Prometheus Server requirements, we haven't enforced it in code.
            // This is because we have implemented Prometheus using a Push Controller, where
            // we accumulate metrics from each Push into exporter, and is used to construct
            // out for /metrics call. Because of this, its possible that multiple Push has occured
            // before Prometheus server makes /metrics call. (i.e Prometheus scrape interval is much more
            // than Push interval scenario)
            // Once a pull model is implemented, we'll not have this issue and we need to add tests
            // at that time.

            // If in future, there is a official .NET Prometheus Client library, and OT Exporter
            // choses to take a dependency on it, then none of these concerns arise.
        }
    }
}
