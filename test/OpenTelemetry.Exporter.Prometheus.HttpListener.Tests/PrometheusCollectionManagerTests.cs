// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Text;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
#if PROMETHEUS_HTTP_LISTENER
using OpenTelemetry.Tests;
#endif

namespace OpenTelemetry.Exporter.Prometheus.Tests;

[Collection(TestMeterCollection.Name)]
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
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(x =>
            {
                x.Port = TcpPortProvider.GetOpenPort();
                x.ScrapeResponseCacheDurationMilliseconds = scrapeResponseCacheDurationMilliseconds;
            })
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

        var startUtc = DateTime.UtcNow;
        var utcNow = startUtc;

        if (cacheEnabled)
        {
            exporter.CollectionManager.UtcNow = () => utcNow;
            exporter.CollectionManager.GetElapsedTime = () => utcNow - startUtc;
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

            var protocol = GetProtocol(openMetricsRequested);
            var response = await exporter.CollectionManager.EnterCollect(protocol);

            try
            {
                return new()
                {
                    CollectionResponse = response,
                    ViewPayload = [.. response.View],
                };
            }
            finally
            {
                exporter.CollectionManager.ExitCollect(response);
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

            await parallel.WaitAsync(cts.Token);

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

        var protocol = GetProtocol(openMetricsRequested);

        // This should use the cache and ignore the second counter update.
        var task = exporter.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        if (cacheEnabled)
        {
            // A cache hit is resolved synchronously without starting a new collection.
            Assert.True(task.IsCompleted, "Collection did not complete.");
        }

        var secondResponse = await task;
        exporter.CollectionManager.ExitCollect(secondResponse);

        if (cacheEnabled)
        {
            Assert.Equal(1, runningCollectCount);
            Assert.True(secondResponse.FromCache, "Response was not served from the cache.");
            Assert.Equal(firstResponse.CollectionResponse.GeneratedAtUtc, secondResponse.GeneratedAtUtc);
        }
        else
        {
            Assert.Equal(2, runningCollectCount);
            Assert.False(secondResponse.FromCache, "Response was served from the cache.");
            Assert.True(firstResponse.CollectionResponse.GeneratedAtUtc < secondResponse.GeneratedAtUtc);
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
    public async Task EnterCollectDoesNotWaitForActiveReadersToExit()
    {
        var timeout = TimeSpan.FromSeconds(30);

        using var meter = CreateMeter();
        using var provider = CreateMeterProviderWithCache(meter, scrapeResponseCacheDurationMilliseconds: 0);
#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        var exporter = GetExporter(provider);
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        var collectCount = CountCollections(exporter);

        var counter = meter.CreateCounter<int>("counter_int");
        counter.Add(100);

        var protocol = GetProtocol(openMetricsRequested: false);

        // Hold a reader on the first response, standing in for a client which does not read it
        var firstResponse = await EnterCollectAsync(exporter, protocol);
        var secondResponse = default(PrometheusCollectionManager.CollectionResponse);

        try
        {
            Assert.True(firstResponse.Succeeded);
            Assert.Equal(1, collectCount());

            var firstPayload = firstResponse.View.ToArray();

            counter.Add(100);

            // The next scrape collects again straight away. Time-boxed, because if it waited for
            // the reader above (as it did before the fix) it would never complete.
#pragma warning disable CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
            var secondScrape = EnterCollectAsync(exporter, protocol);
#pragma warning restore CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
            var completed = await Task.WhenAny(secondScrape, Task.Delay(timeout, TestContext.Current.CancellationToken));

            Assert.Same(secondScrape, completed);

            secondResponse = await secondScrape;

            Assert.True(secondResponse.Succeeded);
            Assert.False(secondResponse.FromCache, "Response was served from the cache.");
            Assert.Equal(2, collectCount());
            Assert.True(firstResponse.GeneratedAtUtc < secondResponse.GeneratedAtUtc);

            // The new response was not written over the one still being read
            Assert.Equal(firstPayload, firstResponse.View.ToArray());
            Assert.NotEqual(firstPayload, secondResponse.View.ToArray());
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(firstResponse);
            exporter.CollectionManager.ExitCollect(secondResponse);
        }

        Assert.Equal(0, exporter.CollectionManager.ActiveReaderCount);
    }

    [Fact]
    public async Task EnterCollectServesMostRecentResponseWhileEveryResponseBufferIsBeingRead()
    {
        var timeout = TimeSpan.FromSeconds(30);

        using var meter = CreateMeter();
        using var provider = CreateMeterProviderWithCache(meter, scrapeResponseCacheDurationMilliseconds: 0);
#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        var exporter = GetExporter(provider);
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        var collectCount = CountCollections(exporter);

        var counter = meter.CreateCounter<int>("counter_int");
        counter.Add(100);

        var protocol = GetProtocol(openMetricsRequested: false);

        async Task<PrometheusCollectionManager.CollectionResponse> EnterWithinTimeoutAsync()
        {
#pragma warning disable CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
            var scrape = EnterCollectAsync(exporter, protocol);
#pragma warning restore CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
            var completed = await Task.WhenAny(scrape, Task.Delay(timeout, TestContext.Current.CancellationToken));

            Assert.Same(scrape, completed);

            return await scrape;
        }

        // Hold readers on two responses from different collections, so that neither of the
        // buffers a response can be written to is free.
        var readers = new List<PrometheusCollectionManager.CollectionResponse>();

        try
        {
            var first = await EnterWithinTimeoutAsync();
            readers.Add(first);

            counter.Add(100);

            var second = await EnterWithinTimeoutAsync();
            readers.Add(second);

            Assert.Equal(2, collectCount());

            var secondPayload = second.View.ToArray();

            counter.Add(100);

            // There is nowhere to write a new response, so the most recent one is served
            // straight away rather than waiting for the readers of the older one to exit.
#pragma warning disable CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
            var thirdScrape = EnterCollectAsync(exporter, protocol);
#pragma warning restore CA2025 // The test awaits the scheduled work before disposing the provider/exporter.

            Assert.True(thirdScrape.IsCompleted, "The scrape waited for active readers to exit.");

            var third = await thirdScrape;
            readers.Add(third);

            Assert.True(third.Succeeded);
            Assert.True(third.FromCache, "Response was not served from the previous collection.");
            Assert.Equal(2, collectCount());
            Assert.Equal(second.GeneratedAtUtc, third.GeneratedAtUtc);
            Assert.Equal(secondPayload, third.View.ToArray());

            // Once the readers of the older response exit, its buffer is written again, while
            // the most recent response is still being read.
            exporter.CollectionManager.ExitCollect(first);
            readers.Remove(first);

            var fourth = await EnterWithinTimeoutAsync();
            readers.Add(fourth);

            Assert.True(fourth.Succeeded);
            Assert.False(fourth.FromCache, "Response was served from the cache.");
            Assert.Equal(3, collectCount());
            Assert.True(third.GeneratedAtUtc < fourth.GeneratedAtUtc);
            Assert.NotEqual(secondPayload, fourth.View.ToArray());
            Assert.Equal(secondPayload, second.View.ToArray());
        }
        finally
        {
            foreach (var reader in readers)
            {
                exporter.CollectionManager.ExitCollect(reader);
            }
        }

        Assert.Equal(0, exporter.CollectionManager.ActiveReaderCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnterCollectSharesActiveCollectionAcrossProtocols(bool firstOpenMetricsRequested)
    {
        using var meter = CreateMeter();
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
        var firstCollectStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondCollectStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstCollectToContinue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var originalCollect = exporter.Collect;
        exporter.Collect = (timeout) =>
        {
            var currentCollectCount = Interlocked.Increment(ref collectCount);

            if (currentCollectCount == 1)
            {
                firstCollectStarted.SetResult(true);

                var completed = allowFirstCollectToContinue.Task.Wait(TimeSpan.FromSeconds(5));
                Assert.True(completed, "First collection did not resume.");
            }

            return originalCollect!(timeout);
        };

        var counter = meter.CreateCounter<int>("counter_int");
        counter.Add(100);

        var firstProtocol = GetProtocol(firstOpenMetricsRequested);
        var secondProtocol = GetProtocol(!firstOpenMetricsRequested);

        var firstCollectTask = Task.Run(async () => await EnterCollectAsync(exporter, firstProtocol));

        await firstCollectStarted.Task;

#pragma warning disable CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
        var secondCollectTask = Task.Run(async () =>
        {
            var collectTask = EnterCollectAsync(exporter, secondProtocol);
            secondCollectStarted.SetResult(true);
            return await collectTask;
        });
#pragma warning restore CA2025 // The test awaits the scheduled work before disposing the provider/exporter.

        await secondCollectStarted.Task;

        Assert.False(secondCollectTask.IsCompleted, "Second collection completed while the first protocol was still collecting.");

        allowFirstCollectToContinue.SetResult(true);

        var timeout = TimeSpan.FromSeconds(5);

        using (var cts = new CancellationTokenSource(timeout))
        {
            var all = Task.WhenAll(firstCollectTask, secondCollectTask);
            var completion = await Task.WhenAny(all, Task.Delay(timeout, cts.Token));
            Assert.Same(all, completion);
        }

        var firstResponse = await firstCollectTask;
        var secondResponse = await secondCollectTask;

        try
        {
            Assert.Equal(1, collectCount);
            Assert.Equal(firstResponse.GeneratedAtUtc, secondResponse.GeneratedAtUtc);

            var firstPayload = Encoding.UTF8.GetString(firstResponse.View.Array!, firstResponse.View.Offset, firstResponse.View.Count);
            var secondPayload = Encoding.UTF8.GetString(secondResponse.View.Array!, secondResponse.View.Offset, secondResponse.View.Count);

            Assert.NotEqual(firstPayload, secondPayload);

            var openMetricsPayload = firstOpenMetricsRequested ? firstPayload : secondPayload;
            var prometheusPayload = firstOpenMetricsRequested ? secondPayload : firstPayload;

            Assert.Contains("# TYPE counter_int counter", openMetricsPayload, StringComparison.Ordinal);
            Assert.Contains("counter_int_created", openMetricsPayload, StringComparison.Ordinal);
            Assert.Contains("# TYPE counter_int_total counter", prometheusPayload, StringComparison.Ordinal);
            Assert.DoesNotContain("counter_int_created", prometheusPayload, StringComparison.Ordinal);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(firstResponse);
            exporter.CollectionManager.ExitCollect(secondResponse);
        }
    }

    [Fact]
    public async Task EnterCollectSharesCollectionsAcrossProtocolsScrapedTogether()
    {
        const int ScrapeResponseCacheDurationMilliseconds = 300;

        using var meter = CreateMeter();
        using var provider = CreateMeterProviderWithCache(meter, ScrapeResponseCacheDurationMilliseconds);
#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        var exporter = GetExporter(provider);
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        var elapsed = TimeSpan.Zero;
        UseManualClock(exporter, () => elapsed);

        var collectCount = CountCollections(exporter);

        meter.CreateCounter<int>("counter_int").Add(100);

        var prometheusProtocol = GetProtocol(openMetricsRequested: false);
        var openMetricsProtocol = GetProtocol(openMetricsRequested: true);
        var fallbackProtocol = PrometheusProtocol.Fallback;

        async Task<PrometheusCollectionManager.CollectionResponse> ScrapeAsync(PrometheusProtocol protocol, int elapsedMilliseconds)
        {
            elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
            return await ScrapeAndExitAsync(exporter, protocol);
        }

        // Each format requested for the first time is collected for on its own.
        var response = await ScrapeAsync(prometheusProtocol, 0);
        Assert.False(response.FromCache, "Response was served from the cache.");
        Assert.Equal(1, collectCount());

        response = await ScrapeAsync(fallbackProtocol, 0);
        Assert.False(response.FromCache, "Response was served from the cache.");
        Assert.Equal(2, collectCount());

        response = await ScrapeAsync(openMetricsProtocol, 100);
        Assert.False(response.FromCache, "Response was served from the cache.");
        Assert.Equal(3, collectCount());

        foreach (var protocol in new[] { prometheusProtocol, fallbackProtocol, openMetricsProtocol })
        {
            response = await ScrapeAsync(protocol, 200);
            Assert.True(response.FromCache, "Response was not served from the cache.");
        }

        Assert.Equal(3, collectCount());

        // Once the cache has expired, the next collection also writes every format whose
        // scrapes arrived alongside a previous collection, including after an idle interval
        // longer than the cache duration.
        foreach (var elapsedMilliseconds in new[] { 450, 1000 })
        {
            response = await ScrapeAsync(prometheusProtocol, elapsedMilliseconds);
            Assert.False(response.FromCache, "Response was served from the cache.");

            var generatedAtUtc = response.GeneratedAtUtc;

            foreach (var protocol in new[] { openMetricsProtocol, fallbackProtocol })
            {
                response = await ScrapeAsync(protocol, elapsedMilliseconds);
                Assert.True(response.FromCache, "Response was not served from the cache.");
                Assert.Equal(generatedAtUtc, response.GeneratedAtUtc);
            }
        }

        Assert.Equal(5, collectCount());

        // A format which was written speculatively and then not requested is not written again.
        response = await ScrapeAsync(prometheusProtocol, 2000);
        Assert.False(response.FromCache, "Response was served from the cache.");
        Assert.Equal(6, collectCount());

        response = await ScrapeAsync(fallbackProtocol, 2000);
        Assert.True(response.FromCache, "Response was not served from the cache.");

        response = await ScrapeAsync(prometheusProtocol, 3000);
        Assert.False(response.FromCache, "Response was served from the cache.");
        Assert.Equal(7, collectCount());

        response = await ScrapeAsync(fallbackProtocol, 3000);
        Assert.True(response.FromCache, "Response was not served from the cache.");

        response = await ScrapeAsync(openMetricsProtocol, 3000);
        Assert.False(response.FromCache, "Response was served from the cache.");
        Assert.Equal(8, collectCount());
    }

    [Fact]
    public async Task EnterCollectSharesCollectionsAcrossProtocolsScrapedPeriodically()
    {
        const int ScrapeResponseCacheDurationMilliseconds = 300;

        using var meter = CreateMeter();
        using var provider = CreateMeterProviderWithCache(meter, ScrapeResponseCacheDurationMilliseconds);
#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        var exporter = GetExporter(provider);
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        var elapsed = TimeSpan.Zero;
        UseManualClock(exporter, () => elapsed);

        var collectCount = CountCollections(exporter);

        meter.CreateCounter<int>("counter_int").Add(100);

        var prometheusProtocol = GetProtocol(openMetricsRequested: false);
        var openMetricsProtocol = GetProtocol(openMetricsRequested: true);

        async Task<PrometheusCollectionManager.CollectionResponse> ScrapeAsync(PrometheusProtocol protocol, int elapsedMilliseconds)
        {
            elapsed = TimeSpan.FromMilliseconds(elapsedMilliseconds);
            return await ScrapeAndExitAsync(exporter, protocol);
        }

        var response = await ScrapeAsync(prometheusProtocol, 0);
        Assert.False(response.FromCache, "Response was served from the cache.");

        var prometheusGeneratedAtUtc = response.GeneratedAtUtc;

        response = await ScrapeAsync(openMetricsProtocol, 1);
        Assert.False(response.FromCache, "Response was served from the cache.");
        Assert.Equal(2, collectCount());

        // The collection for the second format does not rewrite the first format's cached response.
        response = await ScrapeAsync(prometheusProtocol, 2);
        Assert.True(response.FromCache, "Response was not served from the cache.");
        Assert.Equal(prometheusGeneratedAtUtc, response.GeneratedAtUtc);

        // Two scrapers polling together at an interval longer than the cache duration then
        // share one collection per interval.
        for (var interval = 1; interval <= 3; interval++)
        {
            response = await ScrapeAsync(prometheusProtocol, interval * 1000);
            Assert.False(response.FromCache, "Response was served from the cache.");

            var generatedAtUtc = response.GeneratedAtUtc;

            response = await ScrapeAsync(openMetricsProtocol, (interval * 1000) + 1);
            Assert.True(response.FromCache, "Response was not served from the cache.");
            Assert.Equal(generatedAtUtc, response.GeneratedAtUtc);

            Assert.Equal(2 + interval, collectCount());
        }
    }

    [Fact]
    public async Task EnterCollectServesCachedResponseWhileCollectingAnotherProtocol()
    {
        const int ScrapeResponseCacheDurationMilliseconds = 15_000;

        using var meter = CreateMeter();
        using var provider = CreateMeterProviderWithCache(meter, ScrapeResponseCacheDurationMilliseconds);
#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        var exporter = GetExporter(provider);
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        var elapsed = TimeSpan.Zero;
        UseManualClock(exporter, () => elapsed);

        var collectCount = 0;
        var secondCollectStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSecondCollectToContinue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var originalCollect = exporter.Collect;
        exporter.Collect = (timeout) =>
        {
            if (Interlocked.Increment(ref collectCount) == 2)
            {
                // Stands in for a slow observable instrument callback.
                secondCollectStarted.SetResult(true);
                Assert.True(allowSecondCollectToContinue.Task.Wait(TimeSpan.FromSeconds(5)), "Second collection did not resume.");
            }

            return originalCollect!(timeout);
        };

        meter.CreateCounter<int>("counter_int").Add(100);

        var prometheusProtocol = GetProtocol(openMetricsRequested: false);
        var openMetricsProtocol = GetProtocol(openMetricsRequested: true);

        var response = await ScrapeAndExitAsync(exporter, prometheusProtocol);
        Assert.False(response.FromCache, "Response was served from the cache.");

        var generatedAtUtc = response.GeneratedAtUtc;

        elapsed = TimeSpan.FromMilliseconds(100);

        var timeout = TimeSpan.FromSeconds(5);

#pragma warning disable CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
        var collectingScrape = Task.Run(() => ScrapeAndExitAsync(exporter, openMetricsProtocol));
#pragma warning restore CA2025 // The test awaits the scheduled work before disposing the provider/exporter.

        try
        {
            var started = await Task.WhenAny(secondCollectStarted.Task, Task.Delay(timeout, TestContext.Current.CancellationToken));
            Assert.Same(secondCollectStarted.Task, started);

            // The cached response is still valid, so it is served without waiting for the
            // collection for the other format.
#pragma warning disable CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
            var cachedScrape = EnterCollectAsync(exporter, prometheusProtocol);
#pragma warning restore CA2025 // The test awaits the scheduled work before disposing the provider/exporter.

            Assert.True(cachedScrape.IsCompleted, "The cached scrape waited for the collection for another format.");

            response = await cachedScrape;

            try
            {
                Assert.True(response.FromCache, "Response was not served from the cache.");
                Assert.Equal(generatedAtUtc, response.GeneratedAtUtc);
            }
            finally
            {
                exporter.CollectionManager.ExitCollect(response);
            }
        }
        finally
        {
            allowSecondCollectToContinue.TrySetResult(true);
        }

        var completed = await Task.WhenAny(collectingScrape, Task.Delay(timeout, TestContext.Current.CancellationToken));
        Assert.Same(collectingScrape, completed);

        response = await collectingScrape;
        Assert.False(response.FromCache, "Response was served from the cache.");
        Assert.Equal(2, collectCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnterCollectServesCachedResponseWhileSerializingAnotherProtocol(bool repeatCachedRequest)
    {
        using var meter = CreateMeter();
        using var provider = CreateMeterProviderWithCache(meter, 15_000);
#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        var exporter = GetExporter(provider);
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        var elapsed = TimeSpan.Zero;
        UseManualClock(exporter, () => elapsed);
        var collectCount = CountCollections(exporter);

        var serializationStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowSerializationToContinue = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var blockSerialization = false;
        var timeout = TimeSpan.FromSeconds(5);
        var labelValue = new CallbackLabelValue(() =>
        {
            if (blockSerialization)
            {
                serializationStarted.TrySetResult(true);
                Assert.True(allowSerializationToContinue.Task.Wait(timeout), "Serialization did not resume.");
            }

            return "small";
        });

        meter.CreateCounter<int>("counter_int").Add(100, new KeyValuePair<string, object?>("label", labelValue));

        var prometheusProtocol = GetProtocol(openMetricsRequested: false);
        var openMetricsProtocol = GetProtocol(openMetricsRequested: true);
        var response = await ScrapeAndExitAsync(exporter, prometheusProtocol);
        var generatedAtUtc = response.GeneratedAtUtc;
        var payload = response.View.ToArray();

        if (repeatCachedRequest)
        {
            elapsed = TimeSpan.FromMilliseconds(1);
            Assert.True((await ScrapeAndExitAsync(exporter, prometheusProtocol)).FromCache);
        }

        elapsed = TimeSpan.FromMilliseconds(100);
        blockSerialization = true;
        var cachedScrapeCompleted = false;

#pragma warning disable CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
        var collectingScrape = ScrapeAndExitAsync(exporter, openMetricsProtocol);
#pragma warning restore CA2025 // The test awaits the scheduled work before disposing the provider/exporter.

        try
        {
            var started = await Task.WhenAny(serializationStarted.Task, Task.Delay(timeout, TestContext.Current.CancellationToken));
            Assert.Same(serializationStarted.Task, started);

#pragma warning disable CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
            var cachedScrape = EnterCollectAsync(exporter, prometheusProtocol);
#pragma warning restore CA2025 // The test awaits the scheduled work before disposing the provider/exporter.

            // Observe availability while serialization is blocked, then release it before
            // awaiting either scrape so a regression cannot strand a reader or worker.
            cachedScrapeCompleted = cachedScrape.IsCompleted;
            allowSerializationToContinue.TrySetResult(true);

            response = await cachedScrape;
            exporter.CollectionManager.ExitCollect(response);
        }
        finally
        {
            allowSerializationToContinue.TrySetResult(true);
            await collectingScrape;
        }

        Assert.True(cachedScrapeCompleted, "The cached scrape waited for serialization of another format.");
        Assert.True(response.FromCache, "Response was not served from the cache.");
        Assert.Equal(generatedAtUtc, response.GeneratedAtUtc);
        Assert.Equal(payload, response.View.ToArray());
        Assert.Equal(2, collectCount());
        Assert.Equal(0, exporter.CollectionManager.ActiveReaderCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EnterCollectKeepsCachedResponseWhenAnotherProtocolExceedsResponseLimit(bool repeatCachedRequest)
    {
        using var meter = CreateMeter();
#if PROMETHEUS_HTTP_LISTENER
        using var provider = CreateMeterProviderWithRandomPort(meter, options =>
        {
            options.ScrapeResponseCacheDurationMilliseconds = 15_000;
            options.MaxScrapeResponseSizeBytes = PrometheusExporterOptions.InitialScrapeResponseSizeBytes;
        });
#elif PROMETHEUS_ASPNETCORE
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddPrometheusExporter(options =>
            {
                options.ScrapeResponseCacheDurationMilliseconds = 15_000;
                options.MaxScrapeResponseSizeBytes = PrometheusExporterOptions.InitialScrapeResponseSizeBytes;
            })
            .Build();
#endif

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        var exporter = GetExporter(provider);
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        var elapsed = TimeSpan.Zero;
        UseManualClock(exporter, () => elapsed);
        var collectCount = CountCollections(exporter);

        var counter = meter.CreateCounter<int>("counter_int");
        counter.Add(100, new KeyValuePair<string, object?>("label", "small"));

        var prometheusProtocol = GetProtocol(openMetricsRequested: false);
        var openMetricsProtocol = GetProtocol(openMetricsRequested: true);
        var response = await ScrapeAndExitAsync(exporter, prometheusProtocol);
        var generatedAtUtc = response.GeneratedAtUtc;
        var payload = response.View.ToArray();

        if (repeatCachedRequest)
        {
            elapsed = TimeSpan.FromMilliseconds(1);
            Assert.True((await ScrapeAndExitAsync(exporter, prometheusProtocol)).FromCache);
        }

        // The new series makes the next response exceed the configured buffer budget.
        counter.Add(1, new KeyValuePair<string, object?>("label", new string('x', 90_000)));
        elapsed = TimeSpan.FromMilliseconds(100);

        response = await exporter.CollectionManager.EnterCollect(openMetricsProtocol, TestContext.Current.CancellationToken);
        exporter.CollectionManager.ExitCollect(response);

        Assert.False(response.Succeeded, "The oversized scrape unexpectedly succeeded.");

        response = await ScrapeAndExitAsync(exporter, prometheusProtocol);
        Assert.True(response.FromCache, "Response was not served from the cache.");
        Assert.Equal(generatedAtUtc, response.GeneratedAtUtc);
        Assert.Equal(payload, response.View.ToArray());
        Assert.Equal(2, collectCount());
        Assert.Equal(0, exporter.CollectionManager.ActiveReaderCount);
    }

    [Fact]
    public async Task EnterCollectFailsScrapesWhichSharedAFailedCollection()
    {
        var testTimeout = TimeSpan.FromSeconds(30);
        using var cts = new CancellationTokenSource(testTimeout);

        using var meter = CreateMeter();
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
        var firstCollectStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstCollectToComplete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var originalCollect = exporter.Collect;
        exporter.Collect = (timeout) =>
        {
            var currentCollectCount = Interlocked.Increment(ref collectCount);

            if (currentCollectCount == 1)
            {
                firstCollectStarted.SetResult(true);
                Assert.True(allowFirstCollectToComplete.Task.Wait(TimeSpan.FromSeconds(5)), "First collection did not resume.");
                return false;
            }

            return originalCollect!(timeout);
        };

        meter.CreateCounter<int>("counter_int").Add(100);

        var protocol = GetProtocol(openMetricsRequested: false);

        var collectingScrape = Task.Run(async () =>
        {
            var response = await EnterCollectAsync(exporter, protocol);
            try
            {
                return response;
            }
            finally
            {
                exporter.CollectionManager.ExitCollect(response);
            }
        });

        await firstCollectStarted.Task;

        // Share the collection which is about to be failed. EnterCollect registers this
        // scrape with the in-flight collection before it returns the task to wait on, so
        // by the time this call returns the collection is being shared and releasing it
        // below cannot race with joining it.
#pragma warning disable CA2025 // The test awaits the scheduled work before disposing the provider/exporter.
        var sharingScrape = EnterCollectAsync(exporter, protocol);
#pragma warning restore CA2025 // The test awaits the scheduled work before disposing the provider/exporter.

        allowFirstCollectToComplete.SetResult(true);

        var all = Task.WhenAll(collectingScrape, sharingScrape);
        var completion = await Task.WhenAny(all, Task.Delay(testTimeout, cts.Token));

        Assert.Same(all, completion);

        var collectingResponse = await collectingScrape;

        var sharingResponse = await sharingScrape;
        exporter.CollectionManager.ExitCollect(sharingResponse);

        // A failed collection is reported to every scrape which shared it, and is not
        // collected again on their behalf.
        Assert.Equal(1, collectCount);

        Assert.False(collectingResponse.Succeeded, "The scrape which ran the failed collection did not report failure.");
        Assert.Equal(0, collectingResponse.View.Count);

        Assert.False(sharingResponse.Succeeded, "The scrape which shared the failed collection did not report failure.");
        Assert.Equal(0, sharingResponse.View.Count);
    }

    [Fact]
    public async Task EnterCollectRetryPathDoesNotOverflowStackUnderContention()
    {
        var testTimeout = TimeSpan.FromMinutes(2);
        using var cts = new CancellationTokenSource(testTimeout);
        using var stopScraping = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        using var meter = CreateMeter();
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

        meter.CreateCounter<int>("counter_int").Add(100);

        // Make every collection fail immediately, without invoking the real collect
        // pipeline. A failed collection produces no response for any waiting scrape,
        // so joiners are always unsatisfied and must retry, and because it returns
        // synchronously the awaited collection is already complete when the retry resumes.
        exporter.Collect = _ => false;

        var protocol = GetProtocol(openMetricsRequested: false);

        // Enough concurrency to reliably drive the contended retry path, but capped so the
        // test does not spawn an unreasonable number of hot tasks on high-core CI agents.
        var workerCount = Math.Min(Math.Max(Environment.ProcessorCount * 2, 8), 32);

        async Task ScrapeUntilStoppedAsync()
        {
            while (!stopScraping.IsCancellationRequested)
            {
                cts.Token.ThrowIfCancellationRequested();

                var response = await EnterCollectAsync(exporter, protocol);

                try
                {
                    Assert.False(response.Succeeded, "Expected the forced-failure collection to report failure.");
                }
                finally
                {
                    exporter.CollectionManager.ExitCollect(response);
                }
            }
        }

        var workers = new Task[workerCount];
        for (var i = 0; i < workers.Length; i++)
        {
            workers[i] = Task.Run(ScrapeUntilStoppedAsync, cts.Token);
        }

        var all = Task.WhenAll(workers);
        var completion = await Task.WhenAny(all, Task.Delay(testTimeout, cts.Token));

        Assert.Same(all, completion);

        // Observe any faults from the workers.
        await all;
    }

    [Fact]
    public async Task EnterCollectSharesFailedSerializationResult()
    {
        using var meter = CreateMeter();
#if PROMETHEUS_HTTP_LISTENER
        using var provider = CreateMeterProviderWithRandomPort(
            meter,
            options => options.MaxScrapeResponseSizeBytes = PrometheusExporterOptions.InitialScrapeResponseSizeBytes);
#elif PROMETHEUS_ASPNETCORE
        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddPrometheusExporter(options =>
            {
                options.MaxScrapeResponseSizeBytes = PrometheusExporterOptions.InitialScrapeResponseSizeBytes;
                options.ScrapeResponseCacheDurationMilliseconds = 0;
            })
            .Build();
#endif

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        var collectCount = 0;
        var firstCollectStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowFirstCollectToComplete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var originalCollect = exporter!.Collect;
        exporter.Collect = (timeout) =>
        {
            var currentCollectCount = Interlocked.Increment(ref collectCount);

            if (currentCollectCount == 1)
            {
                firstCollectStarted.SetResult(true);
                Assert.True(allowFirstCollectToComplete.Task.Wait(TimeSpan.FromSeconds(5)), "First collection did not resume.");
            }

            return originalCollect!(timeout);
        };

        var counter = meter.CreateCounter<long>("test_counter", description: "Help text.");
        const int SeriesCount = 4_000;
        for (var i = 0; i < SeriesCount; i++)
        {
            counter.Add(
                1234567890123456789L,
                new KeyValuePair<string, object?>("index", $"series-value-{i:D8}-padding-padding-padding"));
        }

        var protocol = GetProtocol(openMetricsRequested: false);

        async Task<PrometheusCollectionManager.CollectionResponse> CollectAsync()
        {
            var response = await EnterCollectAsync(exporter, protocol);
            try
            {
                return response;
            }
            finally
            {
                exporter.CollectionManager.ExitCollect(response);
            }
        }

        var firstCollectTask = Task.Run(CollectAsync);

        await firstCollectStarted.Task;

        var secondCollectTask = CollectAsync();

        Assert.False(secondCollectTask.IsCompleted, "Second collection did not join the active collection.");

        allowFirstCollectToComplete.SetResult(true);

        var timeout = TimeSpan.FromSeconds(5);
        var all = Task.WhenAll(firstCollectTask, secondCollectTask);

        using (var cts = new CancellationTokenSource(timeout))
        {
            var completion = await Task.WhenAny(all, Task.Delay(timeout, cts.Token));
            Assert.Same(all, completion);
        }

        var responses = await all;

        Assert.Equal(1, collectCount);
        Assert.All(responses, response =>
        {
            Assert.False(response.Succeeded, "Expected the collection to fail.");
            Assert.Equal(0, response.View.Count);
        });
    }

    [Fact]
    public async Task OpenMetricsDoesNotEmitScopeInfoMetricFamily()
    {
        using var meter = new Meter("test_meter", "1.0.0", [new("library.mascot", "dotnetbot")], scope: null);

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource((p) => p.AddAttributes([new("service.name", "prometheus")]))
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(static (options) => options.Port = TcpPortProvider.GetOpenPort())
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        meter.CreateCounter<int>("counter_1").Add(1);

        var protocol = GetProtocol(openMetricsRequested: true);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            var output = Encoding.UTF8.GetString(
                response.View.Array!,
                response.View.Offset,
                response.View.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    [Fact]
    public async Task OpenMetricsDoesNotReserveOtelScopeMetricFamilyNames()
    {
        using var meter = new Meter("test_meter", "1.0.0");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource((p) => p.AddAttributes([new("service.name", "prometheus")]))
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(static (options) => options.Port = TcpPortProvider.GetOpenPort())
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        meter.CreateObservableGauge("otel.scope", () => 1);
        meter.CreateObservableGauge("otel.scope.info", () => 2);

        var protocol = GetProtocol(openMetricsRequested: true);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            var output = Encoding.UTF8.GetString(
                response.View.Array!,
                response.View.Offset,
                response.View.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    [Fact]
    public async Task EnterCollectDoesNotFlowCallersExecutionContextIntoTheCollection()
    {
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(x =>
            {
                x.Port = TcpPortProvider.GetOpenPort();
                x.ScrapeResponseCacheDurationMilliseconds = 0;
            })
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter(x => x.ScrapeResponseCacheDurationMilliseconds = 0)
#endif
            .Build();

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        var asyncLocal = new AsyncLocal<string?>();
        string? observedValue = "not observed";

        meter.CreateObservableGauge("gauge", () =>
        {
            observedValue = asyncLocal.Value;
            return 1;
        });

        // Simulate request-scoped ambient state is only meaningful for the lifetime of this call
        asyncLocal.Value = "request-scoped-value";

        var protocol = GetProtocol(openMetricsRequested: false);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            Assert.True(response.Succeeded);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }

        Assert.Null(observedValue);
    }

    [Fact]
    public async Task DuplicateMetricMetadataIsWrittenOncePerScrape()
    {
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource((p) => p.AddAttributes([new("service.name", "prometheus")]))
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(static (options) => options.Port = TcpPortProvider.GetOpenPort())
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000

        var counter1 = meter.CreateCounter<int>("test.metric", unit: "By", description: "Test help");
        var counter2 = meter.CreateCounter<int>("test-metric", unit: "By", description: "Test help");

        counter1.Add(1, [new("source", "a")]);
        counter2.Add(2, [new("source", "b")]);

        var protocol = GetProtocol(openMetricsRequested: false);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    [Fact]
    public async Task MetricMetadataDiscoveredLaterIsWrittenBeforeSamples()
    {
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource((p) => p.AddAttributes([new("service.name", "prometheus")]))
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(static (options) => options.Port = TcpPortProvider.GetOpenPort())
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000

        var counter1 = meter.CreateCounter<int>("test.metric");
        var counter2 = meter.CreateCounter<int>("test-metric", description: "Test help");

        counter1.Add(1, [new("source", "a")]);
        counter2.Add(2, [new("source", "b")]);

        var protocol = GetProtocol(openMetricsRequested: false);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    [Fact]
    public async Task MetricUnitDiscoveredLaterIsWrittenBeforeSamples()
    {
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource((p) => p.AddAttributes([new("service.name", "prometheus")]))
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(static (options) => options.Port = TcpPortProvider.GetOpenPort())
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000

        var counter1 = meter.CreateCounter<int>("test.metric.bytes");
        var counter2 = meter.CreateCounter<int>("test-metric-bytes", unit: "By");

        counter1.Add(1, [new("source", "a")]);
        counter2.Add(2, [new("source", "b")]);

        var protocol = GetProtocol(openMetricsRequested: false);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    [Fact]
    public async Task MetricHelpAndUnitDiscoveredTogetherLaterAreBothWrittenBeforeSamples()
    {
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource((p) => p.AddAttributes([new("service.name", "prometheus")]))
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(static (options) => options.Port = TcpPortProvider.GetOpenPort())
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000

        var counter1 = meter.CreateCounter<int>("test.metric.bytes");
        var counter2 = meter.CreateCounter<int>("test-metric-bytes", unit: "By", description: "Test help");

        counter1.Add(1, [new("source", "a")]);
        counter2.Add(2, [new("source", "b")]);

        var protocol = GetProtocol(openMetricsRequested: false);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    [Fact]
    public async Task ConflictingMetricTypesAreDroppedFromAScrape()
    {
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource((p) => p.AddAttributes([new("service.name", "prometheus")]))
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(static (options) => options.Port = TcpPortProvider.GetOpenPort())
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000

        var counter = meter.CreateCounter<int>("test.metric");
        meter.CreateObservableGauge("test-metric", () => 1);
        counter.Add(1);

        var protocol = GetProtocol(openMetricsRequested: true);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    [Fact]
    public async Task OpenMetricsWritesMetricFamiliesContiguously()
    {
        var prefix = nameof(this.OpenMetricsWritesMetricFamiliesContiguously);
        using var meter1 = new Meter($"{prefix}.one");
        using var meter2 = new Meter($"{prefix}.two");

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource((p) => p.AddAttributes([new("service.name", "prometheus")]))
            .AddMeter(meter1.Name)
            .AddMeter(meter2.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(static (options) => options.Port = TcpPortProvider.GetOpenPort())
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000

        meter1.CreateObservableGauge("test.metric", () => 1, description: "Test help");
        meter1.CreateObservableGauge("other.metric", () => 3, description: "Other help");
        meter2.CreateObservableGauge("test-metric", () => 2, description: "Test help");

        var protocol = GetProtocol(openMetricsRequested: true);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    [Theory]
    [InlineData(PrometheusProtocol.AllowUtf8Escaping)]
    [InlineData(PrometheusProtocol.DotsEscaping)]
    [InlineData(PrometheusProtocol.UnderscoresEscaping)]
    [InlineData(PrometheusProtocol.ValuesEscaping)]
    public async Task OpenMetricsUnitMetadataIsOnlyWrittenForAnUnderscoreSeparatedSuffixOfTheMetricFamilyName(string escaping)
    {
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(static (options) => options.Port = TcpPortProvider.GetOpenPort())
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        meter.CreateCounter<int>("auth_logins", unit: "logins").Add(1);
        meter.CreateCounter<int>("auth.failedlogins", unit: "logins").Add(1);
        meter.CreateCounter<int>("errors", unit: "errors").Add(1);
        meter.CreateCounter<int>("sales", unit: "total").Add(1);
        meter.CreateHistogram<double>("duration_milliseconds", unit: "s").Record(1);
        meter.CreateObservableGauge("disk.kilobytes", () => 1, unit: "By");

        var protocol = new PrometheusProtocol(
            PrometheusProtocol.OpenMetricsMediaType,
            escaping,
            PrometheusProtocol.OpenMetricsV1,
            isOpenMetrics: true);

        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            // "# UNIT <family> <unit>" lines: the unit MUST be a suffix of the family separated by an underscore.
            var units = output
                .Split('\n')
                .Where(line => line.StartsWith("# UNIT ", StringComparison.Ordinal))
                .Select(line => line.Split(' '))
                .ToArray();

            Assert.Contains(units, p => p[3] == "logins");
            Assert.All(units, p => Assert.EndsWith($"_{p[3]}", p[2].Trim('"'), StringComparison.Ordinal));
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task LargeScrapeExpandsBufferAndReturnsCompleteResponse(bool openMetricsRequested)
    {
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(static (options) => options.Port = TcpPortProvider.GetOpenPort())
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        // Emit enough unique time series that the serialized output is far larger than the
        // 85,000-byte initial scrape buffer, forcing the collection manager to grow it one or
        // more times. On modern .NET the serializer signals an insufficient destination buffer
        // by throwing ArgumentException (rather than IndexOutOfRangeException) from its numeric
        // and string write helpers. The collection manager must treat both exceptions the same
        // way - expand the buffer and retry - otherwise the exception escapes, the response view
        // is cleared, and the scrape silently returns an empty (but successful) payload.
        var counter = meter.CreateCounter<long>("test_counter", description: "Help text.");
        const int SeriesCount = 4000;
        for (var i = 0; i < SeriesCount; i++)
        {
            counter.Add(
                1234567890123456789L,
                new KeyValuePair<string, object?>("index", $"series-value-{i:D8}-padding-padding-padding"));
        }

        var protocol = GetProtocol(openMetricsRequested);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            var view = response.View;

            // A non-empty view that is larger than the initial buffer proves the buffer was
            // expanded successfully while serializing, rather than the scrape failing.
            Assert.True(view.Count > 85_000, $"Expected a buffer expansion, but only {view.Count} bytes were written.");

            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            // Every emitted sample should be present in the response.
            var sampleLineCount = CountOccurrences(output, "test_counter_total{");
            Assert.True(sampleLineCount > 1000, $"Expected many sample lines, but found {sampleLineCount}.");
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    [Theory]
    [InlineData(85_000, false)] // The buffer cannot grow beyond its initial size, so the large scrape is dropped.
    [InlineData(64 * 1024 * 1024, true)] // Ample budget, so the large scrape is served in full.
    public async Task MaxScrapeResponseSizeBytesBoundsTheResponseBuffer(int maxScrapeResponseSizeBytes, bool expectNonEmpty)
    {
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener(options =>
            {
                options.Port = TcpPortProvider.GetOpenPort();
                options.MaxScrapeResponseSizeBytes = maxScrapeResponseSizeBytes;
                options.ScrapeResponseCacheDurationMilliseconds = 0;
            })
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter(options =>
            {
                options.MaxScrapeResponseSizeBytes = maxScrapeResponseSizeBytes;
                options.ScrapeResponseCacheDurationMilliseconds = 0;
            })
#endif
            .Build();

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        // Emit enough unique time series that the serialized output is far larger than the
        // 85,000-byte initial scrape buffer, so serving the scrape requires the buffer to grow.
        var counter = meter.CreateCounter<long>("test_counter", description: "Help text.");
        const int SeriesCount = 4_000;
        for (var i = 0; i < SeriesCount; i++)
        {
            counter.Add(
                1234567890123456789L,
                new KeyValuePair<string, object?>("index", $"series-value-{i:D8}-padding-padding-padding"));
        }

        var protocol = GetProtocol(openMetricsRequested: false);
        var response = await exporter!.CollectionManager.EnterCollect(protocol, TestContext.Current.CancellationToken);

        try
        {
            if (expectNonEmpty)
            {
                // The configured budget is large enough for the buffer to grow and serve the scrape.
                Assert.True(response.Succeeded, "Expected the collection to succeed.");
                Assert.True(response.View.Count > 85_000, $"Expected a complete response, but only {response.View.Count} bytes were written.");
            }
            else
            {
                // The buffer cannot grow past the configured maximum, so the scrape is dropped and a
                // failed (empty) response is returned rather than allocating without bound.
                Assert.False(response.Succeeded, "Expected the collection to fail.");
                Assert.Equal(0, response.View.Count);
            }
        }
        finally
        {
            exporter.CollectionManager.ExitCollect(response);
        }
    }

    private static int CountOccurrences(string value, string substring)
    {
        var count = 0;
        var index = 0;

        while ((index = value.IndexOf(substring, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += substring.Length;
        }

        return count;
    }

    private static PrometheusProtocol GetProtocol(bool openMetricsRequested) => new(
        mediaType: openMetricsRequested ? PrometheusProtocol.OpenMetricsMediaType : PrometheusProtocol.PrometheusTextMediaType,
        escaping: PrometheusProtocol.UnderscoresEscaping,
        version: openMetricsRequested ? PrometheusProtocol.OpenMetricsV1 : PrometheusProtocol.PrometheusV1,
        isOpenMetrics: openMetricsRequested);

    private static Task<PrometheusCollectionManager.CollectionResponse> EnterCollectAsync(PrometheusExporter exporter, PrometheusProtocol protocol) =>
#if NET
        exporter.CollectionManager.EnterCollect(protocol).AsTask();
#else
        exporter.CollectionManager.EnterCollect(protocol);
#endif

    private static Meter CreateMeter([CallerMemberName] string name = "") => new(name);

    private static MeterProvider CreateMeterProviderWithCache(Meter meter, int scrapeResponseCacheDurationMilliseconds) =>
#if PROMETHEUS_HTTP_LISTENER
        CreateMeterProviderWithRandomPort(
            meter,
            options => options.ScrapeResponseCacheDurationMilliseconds = scrapeResponseCacheDurationMilliseconds);
#elif PROMETHEUS_ASPNETCORE
        Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddPrometheusExporter(options => options.ScrapeResponseCacheDurationMilliseconds = scrapeResponseCacheDurationMilliseconds)
            .Build();
#endif

    private static PrometheusExporter GetExporter(MeterProvider provider)
    {
#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        if (!provider.TryFindExporter(out PrometheusExporter? exporter))
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle
        {
            throw new InvalidOperationException("PrometheusExporter could not be found on MeterProvider.");
        }

        return exporter;
    }

    private static void UseManualClock(PrometheusExporter exporter, Func<TimeSpan> getElapsed)
    {
        var startUtc = DateTime.UtcNow;

        exporter.CollectionManager.UtcNow = () => startUtc + getElapsed();
        exporter.CollectionManager.GetElapsedTime = getElapsed;
    }

    private static Func<int> CountCollections(PrometheusExporter exporter)
    {
        var collectCount = 0;
        var originalCollect = exporter.Collect;

        exporter.Collect = (timeout) =>
        {
            Interlocked.Increment(ref collectCount);
            return originalCollect!(timeout);
        };

        return () => Volatile.Read(ref collectCount);
    }

    private static async Task<PrometheusCollectionManager.CollectionResponse> ScrapeAndExitAsync(PrometheusExporter exporter, PrometheusProtocol protocol)
    {
        var response = await EnterCollectAsync(exporter, protocol);
        exporter.CollectionManager.ExitCollect(response);

        Assert.True(response.Succeeded, "The scrape did not succeed.");

        return response;
    }

#if PROMETHEUS_HTTP_LISTENER
    private static MeterProvider CreateMeterProviderWithRandomPort(
        Meter meter,
        Action<PrometheusHttpListenerOptions>? configure = null)
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
                        configure?.Invoke(options);
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

    private sealed class CallbackLabelValue(Func<string> getValue)
    {
        public override string ToString() => getValue();
    }

    private sealed class Response
    {
        public PrometheusCollectionManager.CollectionResponse CollectionResponse;

        public byte[]? ViewPayload;
    }
}
