// <copyright file="InMemoryExporterMetricsExtensionsTests.cs" company="OpenTelemetry Authors">
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

#if NETCOREAPP3_1_OR_GREATER

using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using OpenTelemetry.Metrics;

using Xunit;

namespace OpenTelemetry.Tests.InMemoryExporter
{
    /// <summary>
    /// These tests verify that <see cref="InMemoryExporter"/> works with <see cref="IDeferredMeterProviderBuilder"/>.
    /// </summary>
    public class InMemoryExporterMetricsExtensionsTests
    {
        [Fact]
        public async Task Verify_DeferredMeterProviderBuilder_WithMetric()
        {
            var meterName = Utils.GetCurrentMethodName();
            var exportedItems = new List<Metric>();

            await RunMetricsTest(
                configureServices: services => services.AddOpenTelemetryMetrics(builder => builder
                    .AddMeter(meterName)
                    .AddInMemoryExporter(exportedItems)),
                testAction: () =>
                {
                    using var meter = new Meter(meterName);
                    var counter = meter.CreateCounter<long>("meter");
                    counter.Add(10);
                });

            Assert.Single(exportedItems);
            var metricPointsEnumerator = exportedItems[0].GetMetricPoints().GetEnumerator();
            Assert.True(metricPointsEnumerator.MoveNext());
            Assert.Equal(10, metricPointsEnumerator.Current.GetSumLong());
        }

        [Fact]
        public async Task Verify_DeferredMeterProviderBuilder_WithMetricSnapshot()
        {
            var meterName = Utils.GetCurrentMethodName();
            var exportedItems = new List<MetricSnapshot>();

            await RunMetricsTest(
                configureServices: services => services.AddOpenTelemetryMetrics(builder => builder
                    .AddMeter(meterName)
                    .AddInMemoryExporter(exportedItems)),
                testAction: () =>
                {
                    using var meter = new Meter(meterName);
                    var counter = meter.CreateCounter<long>("meter");
                    counter.Add(10);
                });

            Assert.Single(exportedItems);
            Assert.Equal(10, exportedItems[0].MetricPoints[0].GetSumLong());
        }

        private static async Task RunMetricsTest(Action<IServiceCollection> configureServices, Action testAction)
        {
            using var host = await new HostBuilder()
               .ConfigureWebHost(webBuilder => webBuilder
                   .UseTestServer()
                   .ConfigureServices(configureServices)
                   .Configure(app => app.Run(async ctx =>
                   {
                       testAction.Invoke();

                       var meterProvider = app.ApplicationServices.GetRequiredService<MeterProvider>();
                       meterProvider.ForceFlush();

                       await ctx.Response.WriteAsync("hello world");
                   })))
               .StartAsync();

            using var response = await host.GetTestClient().GetAsync($"/{nameof(RunMetricsTest)}").ConfigureAwait(false);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            await host.StopAsync().ConfigureAwait(false);
        }
    }
}
#endif
