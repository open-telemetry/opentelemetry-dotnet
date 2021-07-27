// <copyright file="MetricTests.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
#if NETCOREAPP3_1
using TestApp.AspNetCore._3._1;
#else
using TestApp.AspNetCore._5._0;
#endif
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
{
    public class MetricTests
        : IClassFixture<WebApplicationFactory<Startup>>, IDisposable
    {
        private readonly WebApplicationFactory<Startup> factory;
        private MeterProvider meterProvider = null;

        public MetricTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public void AddAspNetCoreInstrumentation_BadArgs()
        {
            MeterProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddAspNetCoreInstrumentation());
        }

        [Fact]
        public async Task RequestMetricIsCaptured()
        {
            var metricItems = new List<MetricItem>();
            var metricExporter = new TestExporter<MetricItem>(ProcessExport);

            void ProcessExport(Batch<MetricItem> batch)
            {
                foreach (var metricItem in batch)
                {
                    metricItems.Add(metricItem);
                }
            }

            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .AddMetricProcessor(new PushMetricProcessor(exporter: metricExporter, exportIntervalMs: 10, isDelta: true))
                .Build();

            using (var client = this.factory.CreateClient())
            {
                var response = await client.GetAsync("/api/values");
                response.EnsureSuccessStatusCode();
            }

            // Wait for at least two exporter invocations
            WaitForMetricItems(metricItems, 2);

            this.meterProvider.Dispose();

            // There should be more than one result here since we waited for at least two exporter invocations.
            // The exporter continues to receive a metric even if it has not changed since the last export.
            var requestMetrics = metricItems
                .SelectMany(item => item.Metrics.Where(metric => metric.Name == "http.server.request_count"))
                .ToArray();

            Assert.True(requestMetrics.Length > 1);

            var metric = requestMetrics[0] as ISumMetric;
            Assert.NotNull(metric);
            Assert.Equal(1L, metric.Sum.Value);

            var method = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpMethod, "GET");
            var scheme = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, "http");
            var statusCode = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpStatusCode, 200);
            var flavor = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpFlavor, "HTTP/1.1");
            Assert.Contains(method, metric.Attributes);
            Assert.Contains(scheme, metric.Attributes);
            Assert.Contains(statusCode, metric.Attributes);
            Assert.Contains(flavor, metric.Attributes);
            Assert.Equal(4, metric.Attributes.Length);

            for (var i = 1; i < requestMetrics.Length; ++i)
            {
                metric = requestMetrics[i] as ISumMetric;
                Assert.NotNull(metric);
                Assert.Equal(0L, metric.Sum.Value);
            }
        }

        public void Dispose()
        {
            this.meterProvider?.Dispose();
        }

        private static void WaitForMetricItems(List<MetricItem> metricItems, int count)
        {
            Assert.True(SpinWait.SpinUntil(
                () =>
                {
                    Thread.Sleep(10);
                    return metricItems.Count >= count;
                },
                TimeSpan.FromSeconds(1)));
        }
    }
}
