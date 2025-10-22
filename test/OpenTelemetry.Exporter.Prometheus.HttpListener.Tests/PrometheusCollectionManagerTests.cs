// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Metrics;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public sealed class PrometheusCollectionManagerTests
{
    [Theory]
    [InlineData(0, true)] // disable cache, default value for HttpListener
    [InlineData(0, false)] // disable cache, default value for HttpListener
#if PROMETHEUS_ASPNETCORE
    [InlineData(300, true)] // default value for AspNetCore, no possibility to set on HttpListener
    [InlineData(300, false)] // default value for AspNetCore, no possibility to set on HttpListener
#endif
    public async Task EnterExitCollectTest(int scrapeResponseCacheDurationMilliseconds, bool openMetricsRequested)
    {
        var testTimeout = TimeSpan.FromMinutes(1);
        using var cts = new CancellationTokenSource(testTimeout);

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
#pragma warning disable CA2000 // Dispose objects before losing scope
            if (!provider.TryFindExporter(out PrometheusExporter? exporter))
#pragma warning restore CA2000 // Dispose objects before losing scope
            {
                throw new InvalidOperationException("PrometheusExporter could not be found on MeterProvider.");
            }

            int runningCollectCount = 0;
            var collectFunc = exporter.Collect;
            exporter.Collect = (timeout) =>
            {
                bool result = collectFunc!(timeout);
                runningCollectCount++;

                cts.Token.ThrowIfCancellationRequested();
                Thread.Sleep(5000);

                return result;
            };

            var utcNow = DateTime.UtcNow;

            if (cacheEnabled)
            {
                // Override the cache to ensure the cache is always seen again during its validity period.
                exporter.CollectionManager.UtcNow = () => utcNow;
            }

            var counter = meter.CreateCounter<int>("counter_int", description: "Prometheus help text goes here \n escaping.");
            counter.Add(100);

            Task<Response>[] collectTasks = new Task<Response>[Math.Max(Environment.ProcessorCount / 2, 2)];
            for (int i = 0; i < collectTasks.Length; i++)
            {
                collectTasks[i] = Task.Run(
                    async () =>
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        // Tick the clock forward - it should still be well within the cache duration.
                        utcNow = utcNow.AddMilliseconds(1);

                        var response = await exporter.CollectionManager.EnterCollect(openMetricsRequested, cts.Token);
                        try
                        {
                            return new Response
                            {
                                CollectionResponse = response,
                                ViewPayload = openMetricsRequested ? [.. response.OpenMetricsView] : [.. response.PlainTextView],
                            };
                        }
                        finally
                        {
                            exporter.CollectionManager.ExitCollect();
                        }
                    },
                    cts.Token);
            }

            await WaitForTasksWithTimeout(collectTasks, testTimeout, cts.Token);

            Assert.Equal(1, runningCollectCount);

            var firstResponse = await collectTasks[0];

            Assert.False(firstResponse.CollectionResponse.FromCache, "Response was served from the cache.");

            for (int i = 1; i < collectTasks.Length; i++)
            {
                Assert.Equal(firstResponse.ViewPayload, (await collectTasks[i]).ViewPayload);
                Assert.Equal(firstResponse.CollectionResponse.GeneratedAtUtc, (await collectTasks[i]).CollectionResponse.GeneratedAtUtc);
            }

            counter.Add(100);

            // This should use the cache and ignore the second counter update.
            var task = exporter.CollectionManager.EnterCollect(openMetricsRequested, cts.Token);
            Assert.True(task.IsCompleted, "Collection did not complete.");
            var response = await task;
            try
            {
                if (cacheEnabled)
                {
                    Assert.Equal(1, runningCollectCount);
                    Assert.True(response.FromCache, "Response was not served from the cache.");
                    Assert.Equal(firstResponse.CollectionResponse.GeneratedAtUtc, response.GeneratedAtUtc);
                }
                else
                {
                    Assert.Equal(2, runningCollectCount);
                    Assert.False(response.FromCache, "Response was served from the cache.");
                    Assert.True(firstResponse.CollectionResponse.GeneratedAtUtc < response.GeneratedAtUtc);
                }
            }
            finally
            {
                exporter.CollectionManager.ExitCollect();
            }

            if (cacheEnabled)
            {
                // Progress time beyond the cache duration to force cache expiry.
                utcNow = utcNow.AddMilliseconds(exporter.ScrapeResponseCacheDurationMilliseconds + 1);
            }

            counter.Add(100);

            for (int i = 0; i < collectTasks.Length; i++)
            {
                collectTasks[i] = Task.Run(
                    async () =>
                    {
                        cts.Token.ThrowIfCancellationRequested();

                        var collectionResponse = await exporter.CollectionManager.EnterCollect(openMetricsRequested, cts.Token);
                        try
                        {
                            return new Response
                            {
                                CollectionResponse = collectionResponse,
                                ViewPayload = openMetricsRequested ? [.. collectionResponse.OpenMetricsView] : [.. collectionResponse.PlainTextView],
                            };
                        }
                        finally
                        {
                            exporter.CollectionManager.ExitCollect();
                        }
                    },
                    cts.Token);
            }

            await WaitForTasksWithTimeout(collectTasks, testTimeout, cts.Token);

            Assert.Equal(cacheEnabled ? 2 : 3, runningCollectCount);
            Assert.NotEqual(firstResponse.ViewPayload, (await collectTasks[0]).ViewPayload);
            Assert.NotEqual(firstResponse.CollectionResponse.GeneratedAtUtc, (await collectTasks[0]).CollectionResponse.GeneratedAtUtc);

            firstResponse = await collectTasks[0];

            Assert.False(firstResponse.CollectionResponse.FromCache, "Response was served from the cache.");

            for (int i = 1; i < collectTasks.Length; i++)
            {
                Assert.Equal(firstResponse.ViewPayload, (await collectTasks[i]).ViewPayload);
                Assert.Equal(firstResponse.CollectionResponse.GeneratedAtUtc, (await collectTasks[i]).CollectionResponse.GeneratedAtUtc);
            }
        }

        static async Task WaitForTasksWithTimeout<T>(Task<T>[] tasks, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var all = Task.WhenAll(tasks);
            var finished = await Task.WhenAny(all, Task.Delay(timeout, cancellationToken));

            cancellationToken.ThrowIfCancellationRequested();

            await all;
        }
    }

    private sealed class Response
    {
        public PrometheusCollectionManager.CollectionResponse CollectionResponse;

        public byte[]? ViewPayload;
    }
}
