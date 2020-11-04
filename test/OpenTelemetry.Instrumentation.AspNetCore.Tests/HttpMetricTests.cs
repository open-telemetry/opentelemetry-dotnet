// <copyright file="HttpMetricTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Metrics.Tests;
using OpenTelemetry.Trace;
#if NETCOREAPP2_1
using TestApp.AspNetCore._2._1;
#else
using TestApp.AspNetCore._3._1;
#endif
using Xunit;

namespace OpenTelemetry.Instrumentation.AspNetCore.Tests
{
    public class HttpMetricTests
        : IClassFixture<WebApplicationFactory<Startup>>, IDisposable
    {
        private readonly WebApplicationFactory<Startup> factory;
        private MeterProvider meterProvider = null;

        public HttpMetricTests(WebApplicationFactory<Startup> factory)
        {
            this.factory = factory;
        }

        [Fact]
        public void AddAspNetCoreInstrumentation_BadArgs()
        {
            MeterProviderBuilder builder = null;
            Assert.Throws<ArgumentNullException>(() => builder.AddAspNetCoreInstrumentation());
        }

        [Theory]
        [InlineData("localhost", null)]
        [InlineData("localhost", 5000)]
        public async Task HttpServerDurationMetricIsRecorededWithCorrectLabels(string host, int? port)
        {
            var exportCalledCount = 0;
            var metricExporter = new TestMetricExporter(() => exportCalledCount++);

            void ConfigureTestServices(IServiceCollection services)
            {
                this.meterProvider = Sdk.CreateMeterProviderBuilder()
                    .AddAspNetCoreInstrumentation()
                    .SetProcessor(new TestMetricProcessor())
                    .SetExporter(metricExporter)
                    .SetPushInterval(TimeSpan.FromMilliseconds(100))
                    .Build();

                MeterProvider.SetDefault(this.meterProvider);
            }

            // Arrange
            using (var client = this.factory
                .WithWebHostBuilder(builder =>
                    builder.ConfigureTestServices(ConfigureTestServices))
                .CreateClient(new WebApplicationFactoryClientOptions()
                {
                    BaseAddress = new Uri(port.HasValue ? $"http://{host}:{port}" : $"http://{host}"),
                }))
            {
                // Act
                var response = await client.GetAsync("/api/values");

                // Assert
                response.EnsureSuccessStatusCode(); // Status Code 200-299

                WaitForMetricExport(metricExporter, 1);
            }

            Assert.Single(metricExporter.Metrics);

            var metric = metricExporter.Metrics[0];
            Assert.Equal(SemanticConventions.MetricHttpServerDuration, metric.MetricName);
            Assert.Equal(string.Empty, metric.MetricNamespace);
            Assert.Equal(SemanticConventions.MetricHttpServerDuration, metric.MetricDescription);
            Assert.Equal(AggregationType.DoubleSummary, metric.AggregationType);

            Assert.Single(metric.Data);

            var data = metric.Data[0] as DoubleSummaryData;
            Assert.NotNull(data);
            Assert.True(data.Min > 0);
            Assert.True(data.Max > 0);
            Assert.True(data.Sum > 0);

            var labels = data.Labels.ToDictionary(x => x.Key, x => x.Value);
            Assert.Equal("GET", labels[SemanticConventions.AttributeHttpMethod]);
            Assert.Equal("http", labels[SemanticConventions.AttributeHttpScheme]);
            Assert.Equal(port.HasValue ? $"{host}:{port}" : host, labels[SemanticConventions.AttributeHttpHost]);
            Assert.Equal($"{host}", labels[SemanticConventions.AttributeNetHostName]);

            if (port.HasValue)
            {
                Assert.Equal($"{port}", labels[SemanticConventions.AttributeNetHostPort]);
            }
            else
            {
                Assert.False(labels.ContainsKey(SemanticConventions.AttributeNetHostPort));
            }

            Assert.Equal("200", labels[SemanticConventions.AttributeHttpStatusCode]);

            // By default, HttpFlavor is HTTP/2.0 for netcoreapp2.1 and HTTP/1.1 for netcoreapp3.1
            Assert.Matches("HTTP/\\d\\.\\d", labels[SemanticConventions.AttributeHttpFlavor]);
            Assert.False(labels.ContainsKey(SemanticConventions.AttributeHttpServerName));
            Assert.False(labels.ContainsKey(SemanticConventions.AttributeHttpTarget));
            Assert.False(labels.ContainsKey(SemanticConventions.AttributeHttpUrl));
        }

        public void Dispose()
        {
            this.meterProvider?.Dispose();

            // MeterProvider.Reset is an internal method for use by tests.
            // We cannot apply InternalsVisableTo on OpenTelemetry.Api because its internal
            // SemanticConventions class is also used by OpenTelemetry.Instrumentation.AspNetCore
            var resetMethod = typeof(MeterProvider).GetMethod("Reset", BindingFlags.Static | BindingFlags.NonPublic);
            resetMethod.Invoke(null, null);
        }

        private static void WaitForMetricExport(TestMetricExporter metricExporter, int expectedMetricCount)
        {
            // We need to let End callback execute as it is executed AFTER response was returned.
            // In unit tests environment there may be a lot of parallel unit tests executed, so
            // giving some breezing room for the End callback to complete
            Assert.True(SpinWait.SpinUntil(
                () =>
                {
                    Thread.Sleep(10);
                    return metricExporter.Metrics.Count >= expectedMetricCount;
                },
                TimeSpan.FromSeconds(1)));
        }
    }
}
