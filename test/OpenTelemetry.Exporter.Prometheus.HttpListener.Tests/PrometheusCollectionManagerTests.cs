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
    [InlineData(0, true)] // disable cache
    [InlineData(0, false)] // disable cache
    [InlineData(300, true)] // default value
    [InlineData(300, false)] // default value
    public async Task EnterExitCollectTest(int scrapeResponseCacheDurationMilliseconds, bool openMetricsRequested)
    {
        var testTimeout = TimeSpan.FromMinutes(1);
        using var cts = new CancellationTokenSource(testTimeout);

        var cacheEnabled = scrapeResponseCacheDurationMilliseconds != 0;
        using var meter = new Meter(Utils.GetCurrentMethodName());

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(x => x.ScrapeResponseCacheDurationMilliseconds = scrapeResponseCacheDurationMilliseconds)
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter(x => x.ScrapeResponseCacheDurationMilliseconds = scrapeResponseCacheDurationMilliseconds)
#endif
            .Build();

#pragma warning disable CA2000 // Dispose objects before losing scope
        if (!provider.TryFindExporter(out PrometheusExporter? exporter))
#pragma warning restore CA2000 // Dispose objects before losing scope
        {
            throw new InvalidOperationException("PrometheusExporter could not be found on MeterProvider.");
        }

        var runningCollectCount = 0;
        var collectFunc = exporter.Collect;
        exporter.Collect = (timeout) =>
        {
            var result = collectFunc!(timeout);
            Interlocked.Increment(ref runningCollectCount);

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

        async Task<Response> CollectAsync(bool advanceClock)
        {
            cts.Token.ThrowIfCancellationRequested();

            if (advanceClock)
            {
                // Tick the clock forward - it should still be well within the cache duration.
                utcNow = utcNow.AddMilliseconds(1);
            }

            var response = await exporter.CollectionManager.EnterCollect(openMetricsRequested);
            try
            {
                return new()
                {
                    CollectionResponse = response,
                    ViewPayload = openMetricsRequested ? [.. response.OpenMetricsView] : [.. response.PlainTextView],
                };
            }
            finally
            {
                exporter.CollectionManager.ExitCollect();
            }
        }

        async Task<Task<Response>[]> CollectInParallelAsync(bool advanceClock)
        {
            // Avoid deadlocks by limiting parallelism to a reasonable level based on CPU count.
            // Always use at least 2 to ensure concurrency happens. Running on a single core machine is unlikely.
            var parallelism = Math.Max((Environment.ProcessorCount + 1) / 2, 2);

#if NET
            var bag = new System.Collections.Concurrent.ConcurrentBag<Response>();

            var parallel = Parallel.ForAsync(
                0,
                parallelism,
                cts.Token,
                async (_, _) => bag.Add(await CollectAsync(advanceClock)));

            await Task.WhenAny(parallel, Task.Delay(testTimeout, cts.Token));

            cts.Token.ThrowIfCancellationRequested();

            await parallel;

            return [.. bag.Select((r) => Task.FromResult(r))];
#else

            var tasks = new Task<Response>[parallelism];

            for (var i = 0; i < tasks.Length; i++)
            {
                tasks[i] = Task.Run(() => CollectAsync(advanceClock), cts.Token);
            }

            var all = Task.WhenAll(tasks);
            await Task.WhenAny(all, Task.Delay(testTimeout, cts.Token));

            cts.Token.ThrowIfCancellationRequested();

            await all;

            return tasks;
#endif
        }

        var collectTasks = await CollectInParallelAsync(advanceClock: true);

        Assert.Equal(1, runningCollectCount);

        var firstResponse = await collectTasks[0];

        Assert.False(firstResponse.CollectionResponse.FromCache, "Response was served from the cache.");

        for (var i = 1; i < collectTasks.Length; i++)
        {
            var response = await collectTasks[i];

            Assert.Equal(firstResponse.ViewPayload, response.ViewPayload);
            Assert.Equal(firstResponse.CollectionResponse.GeneratedAtUtc, response.CollectionResponse.GeneratedAtUtc);
        }

        counter.Add(100);

        try
        {
            // This should use the cache and ignore the second counter update.
            var task = exporter.CollectionManager.EnterCollect(openMetricsRequested);
            Assert.True(task.IsCompleted, "Collection did not complete.");
            var response = await task;

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

        collectTasks = await CollectInParallelAsync(advanceClock: false);

        Assert.Equal(cacheEnabled ? 2 : 3, runningCollectCount);

        var original = firstResponse;
        firstResponse = await collectTasks[0];

        Assert.NotEqual(original.ViewPayload, firstResponse.ViewPayload);
        Assert.NotEqual(original.CollectionResponse.GeneratedAtUtc, firstResponse.CollectionResponse.GeneratedAtUtc);

        Assert.False(firstResponse.CollectionResponse.FromCache, "Response was served from the cache.");

        for (var i = 1; i < collectTasks.Length; i++)
        {
            var response = await collectTasks[i];

            Assert.Equal(firstResponse.ViewPayload, response.ViewPayload);
            Assert.Equal(firstResponse.CollectionResponse.GeneratedAtUtc, response.CollectionResponse.GeneratedAtUtc);
        }
    }

    [Fact]
    public async Task EnterCollectWaitsForActiveReadersToExit()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
#if PROMETHEUS_HTTP_LISTENER
        using var provider = CreateMeterProviderWithRandomPort(meter);
#elif PROMETHEUS_ASPNETCORE
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddPrometheusExporter(options => options.ScrapeResponseCacheDurationMilliseconds = 0)
            .Build();
#endif

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        if (!provider.TryFindExporter(out PrometheusExporter? exporter))
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle
        {
            throw new InvalidOperationException("PrometheusExporter could not be found on MeterProvider.");
        }

        var collectCount = 0;
        var secondCollectStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var originalCollect = exporter.Collect;
        exporter.Collect = (timeout) =>
        {
            Interlocked.Increment(ref collectCount);
            return originalCollect!(timeout);
        };

        var counter = meter.CreateCounter<int>("counter_int");
        counter.Add(100);

        var firstResponse = await exporter.CollectionManager.EnterCollect(openMetricsRequested: false);
        var firstCollectExited = false;
        try
        {
            Assert.False(firstResponse.FromCache);
            Assert.Equal(1, collectCount);

            var secondCollectTask = Task.Run(async () =>
            {
                secondCollectStarted.SetResult(true);
                var response = await exporter.CollectionManager.EnterCollect(openMetricsRequested: false);
                try
                {
                    return response;
                }
                finally
                {
                    exporter.CollectionManager.ExitCollect();
                }
            });

            await secondCollectStarted.Task;

            var completion = await Task.WhenAny(secondCollectTask, Task.Delay(TimeSpan.FromSeconds(1)));

            Assert.NotSame(secondCollectTask, completion);
            Assert.False(secondCollectTask.IsCompleted);
            Assert.Equal(1, collectCount);

            exporter.CollectionManager.ExitCollect();
            firstCollectExited = true;

            var timeout = TimeSpan.FromSeconds(5);

#if NET
            await secondCollectTask.WaitAsync(timeout);
#else
            using var cts = new CancellationTokenSource(timeout);
            completion = await Task.WhenAny(secondCollectTask, Task.Delay(timeout, cts.Token));
            Assert.Same(secondCollectTask, completion);
#endif

            var secondResponse = await secondCollectTask;

            Assert.False(secondResponse.FromCache);
            Assert.Equal(2, collectCount);
            Assert.True(firstResponse.GeneratedAtUtc < secondResponse.GeneratedAtUtc);
        }
        finally
        {
            if (!firstCollectExited)
            {
                exporter.CollectionManager.ExitCollect();
            }
        }
    }

#if PROMETHEUS_HTTP_LISTENER
    private static MeterProvider CreateMeterProviderWithRandomPort(Meter meter)
    {
        var retryAttempts = 5;

        while (retryAttempts-- != 0)
        {
            var port = TcpPortProvider.GetOpenPort();

            try
            {
                return Sdk.CreateMeterProviderBuilder()
                    .AddMeter(meter.Name)
                    .AddPrometheusHttpListener((options) =>
                    {
                        options.Port = port;
                        options.ScrapeResponseCacheDurationMilliseconds = 0;
                    })
                    .Build();
            }
            catch (System.Net.HttpListenerException)
            {
                // Retry with another port.
            }
        }

        throw new InvalidOperationException("MeterProvider could not bind a PrometheusHttpListener port.");
    }
#endif

    private sealed class Response
    {
        public PrometheusCollectionManager.CollectionResponse CollectionResponse;

        public byte[]? ViewPayload;
    }
}
