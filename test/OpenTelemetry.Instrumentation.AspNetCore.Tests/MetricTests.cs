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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
#if NETCOREAPP3_1
using TestApp.AspNetCore._3._1;
#endif
#if NET6_0
using TestApp.AspNetCore._6._0;
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
            var metricItems = new List<Metric>();

            this.meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddAspNetCoreInstrumentation()
                .AddInMemoryExporter(metricItems)
                .Build();

            using (var client = this.factory.CreateClient())
            {
                var response = await client.GetAsync("/api/values");
                response.EnsureSuccessStatusCode();
            }

            // We need to let End callback execute as it is executed AFTER response was returned.
            // In unit tests environment there may be a lot of parallel unit tests executed, so
            // giving some breezing room for the End callback to complete
            await Task.Delay(TimeSpan.FromSeconds(1));

            this.meterProvider.Dispose();

            var requestMetrics = metricItems
                .Where(item => item.Name == "http.server.duration")
                .ToArray();

            Assert.True(requestMetrics.Length == 1);

            var metric = requestMetrics[0];
            Assert.NotNull(metric);
            Assert.True(metric.MetricType == MetricType.Histogram);
            var metricPoints = new List<MetricPoint>();
            foreach (var p in metric.GetMetricPoints())
            {
                metricPoints.Add(p);
            }

            Assert.Single(metricPoints);

            var metricPoint = metricPoints[0];

            var count = metricPoint.GetHistogramCount();
            var sum = metricPoint.GetHistogramSum();

            Assert.Equal(1L, count);
            Assert.True(sum > 0);

            /*
            var bucket = metric.Buckets
                .Where(b =>
                    metric.PopulationSum > b.LowBoundary &&
                    metric.PopulationSum <= b.HighBoundary)
                .FirstOrDefault();
            Assert.NotEqual(default, bucket);
            Assert.Equal(1, bucket.Count);
            */

            var attributes = new KeyValuePair<string, object>[metricPoint.Tags.Count];
            int i = 0;
            foreach (var tag in metricPoint.Tags)
            {
                attributes[i++] = tag;
            }

            var method = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpMethod, "GET");
            var scheme = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpScheme, "http");
            var statusCode = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpStatusCode, 200);
            var flavor = new KeyValuePair<string, object>(SemanticConventions.AttributeHttpFlavor, "HTTP/1.1");
            Assert.Contains(method, attributes);
            Assert.Contains(scheme, attributes);
            Assert.Contains(statusCode, attributes);
            Assert.Contains(flavor, attributes);
            Assert.Equal(4, attributes.Length);
        }

        public void Dispose()
        {
            this.meterProvider?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
