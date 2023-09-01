// <copyright file="PrometheusCollectionManagerTests.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public sealed class PrometheusCollectionManagerTests
{
    [Theory]
    [InlineData(0)] // disable cache, default value for HttpListener
#if PROMETHEUS_ASPNETCORE
    [InlineData(300)] // default value for AspNetCore, no possibility to set on HttpListener
#endif
    public async Task EnterExitCollectTest(int scrapeResponseCacheDurationMilliseconds)
    {
        bool cacheEnabled = scrapeResponseCacheDurationMilliseconds != 0;
        using var meter = new Meter(Utils.GetCurrentMethodName());

        using (var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener()
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter(x => x.ScrapeResponseCacheDurationMilliseconds = scrapeResponseCacheDurationMilliseconds)
#endif
            .Build())
        {
            if (!provider.TryFindExporter(out PrometheusExporter exporter))
            {
                throw new InvalidOperationException("PrometheusExporter could not be found on MeterProvider.");
            }

            int runningCollectCount = 0;
            var collectFunc = exporter.Collect;
            exporter.Collect = (timeout) =>
            {
                bool result = collectFunc(timeout);
                runningCollectCount++;
                Thread.Sleep(5000);
                return result;
            };

            var counter = meter.CreateCounter<int>("counter_int", description: "Prometheus help text goes here \n escaping.");
            counter.Add(100);

            Task<Response>[] collectTasks = new Task<Response>[10];
            for (int i = 0; i < collectTasks.Length; i++)
            {
                collectTasks[i] = Task.Run(async () =>
                {
                    var response = await exporter.CollectionManager.EnterCollect().ConfigureAwait(false);
                    try
                    {
                        return new Response
                        {
                            CollectionResponse = response,
                            ViewPayload = response.View.ToArray(),
                        };
                    }
                    finally
                    {
                        exporter.CollectionManager.ExitCollect();
                    }
                });
            }

            await Task.WhenAll(collectTasks).ConfigureAwait(false);

            Assert.Equal(1, runningCollectCount);

            var firstResponse = collectTasks[0].Result;

            Assert.False(firstResponse.CollectionResponse.FromCache);

            for (int i = 1; i < collectTasks.Length; i++)
            {
                Assert.Equal(firstResponse.ViewPayload, collectTasks[i].Result.ViewPayload);
                Assert.Equal(firstResponse.CollectionResponse.GeneratedAtUtc, collectTasks[i].Result.CollectionResponse.GeneratedAtUtc);
            }

            counter.Add(100);

            // This should use the cache and ignore the second counter update.
            var task = exporter.CollectionManager.EnterCollect();
            Assert.True(task.IsCompleted);
            var response = await task.ConfigureAwait(false);
            try
            {
                if (cacheEnabled)
                {
                    Assert.Equal(1, runningCollectCount);
                    Assert.True(response.FromCache);
                    Assert.Equal(firstResponse.CollectionResponse.GeneratedAtUtc, response.GeneratedAtUtc);
                }
                else
                {
                    Assert.Equal(2, runningCollectCount);
                    Assert.False(response.FromCache);
                    Assert.True(firstResponse.CollectionResponse.GeneratedAtUtc < response.GeneratedAtUtc);
                }
            }
            finally
            {
                exporter.CollectionManager.ExitCollect();
            }

            Thread.Sleep(exporter.ScrapeResponseCacheDurationMilliseconds);

            counter.Add(100);

            for (int i = 0; i < collectTasks.Length; i++)
            {
                collectTasks[i] = Task.Run(async () =>
                {
                    var response = await exporter.CollectionManager.EnterCollect().ConfigureAwait(false);
                    try
                    {
                        return new Response
                        {
                            CollectionResponse = response,
                            ViewPayload = response.View.ToArray(),
                        };
                    }
                    finally
                    {
                        exporter.CollectionManager.ExitCollect();
                    }
                });
            }

            await Task.WhenAll(collectTasks).ConfigureAwait(false);

            Assert.Equal(cacheEnabled ? 2 : 3, runningCollectCount);
            Assert.NotEqual(firstResponse.ViewPayload, collectTasks[0].Result.ViewPayload);
            Assert.NotEqual(firstResponse.CollectionResponse.GeneratedAtUtc, collectTasks[0].Result.CollectionResponse.GeneratedAtUtc);

            firstResponse = collectTasks[0].Result;

            Assert.False(firstResponse.CollectionResponse.FromCache);

            for (int i = 1; i < collectTasks.Length; i++)
            {
                Assert.Equal(firstResponse.ViewPayload, collectTasks[i].Result.ViewPayload);
                Assert.Equal(firstResponse.CollectionResponse.GeneratedAtUtc, collectTasks[i].Result.CollectionResponse.GeneratedAtUtc);
            }
        }
    }

    private class Response
    {
        public PrometheusCollectionManager.CollectionResponse CollectionResponse;

        public byte[] ViewPayload;
    }
}
