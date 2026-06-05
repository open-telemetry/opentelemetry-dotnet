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

        try
        {
            var protocol = GetProtocol(openMetricsRequested);

            // This should use the cache and ignore the second counter update.
            var task = exporter.CollectionManager.EnterCollect(protocol);

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
        var secondCollectStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var originalCollect = exporter.Collect;
        exporter.Collect = (timeout) =>
        {
            Interlocked.Increment(ref collectCount);
            return originalCollect!(timeout);
        };

        var counter = meter.CreateCounter<int>("counter_int");
        counter.Add(100);

        var protocol = GetProtocol(openMetricsRequested: false);

        var firstResponse = await exporter.CollectionManager.EnterCollect(protocol);
        var firstCollectExited = false;
        try
        {
            Assert.False(firstResponse.FromCache);
            Assert.Equal(1, collectCount);

            var secondCollectTask = Task.Run(async () =>
            {
                secondCollectStarted.SetResult(true);
                var response = await exporter.CollectionManager.EnterCollect(protocol);
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

            var firstTimeout = TimeSpan.FromSeconds(1);

            using (var cts = new CancellationTokenSource(firstTimeout))
            {
                var completion = await Task.WhenAny(secondCollectTask, Task.Delay(firstTimeout, cts.Token));
                Assert.NotSame(secondCollectTask, completion);
                Assert.False(secondCollectTask.IsCompleted);
            }

            Assert.Equal(1, collectCount);

            exporter.CollectionManager.ExitCollect();
            firstCollectExited = true;

            var secondTimeout = TimeSpan.FromSeconds(5);

            using (var cts = new CancellationTokenSource(secondTimeout))
            {
                var completion = await Task.WhenAny(secondCollectTask, Task.Delay(secondTimeout, cts.Token));
                Assert.Same(secondCollectTask, completion);
            }

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnterCollectRunsDifferentProtocolsSeparately(bool firstOpenMetricsRequested)
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

        var secondCollectTask = Task.Run(async () => await EnterCollectAsync(exporter, secondProtocol));

        Assert.False(secondCollectTask.IsCompleted, "Second collection completed while the first protocol was still collecting.");

        allowFirstCollectToContinue.SetResult(true);

        var firstResponse = await firstCollectTask;
        var firstCollectExited = false;

        try
        {
            var timeout = TimeSpan.FromSeconds(1);

            using (var cts = new CancellationTokenSource(timeout))
            {
                var completion = await Task.WhenAny(secondCollectTask, Task.Delay(timeout, cts.Token));
                Assert.NotSame(secondCollectTask, completion);
                Assert.False(secondCollectTask.IsCompleted);
            }

            Assert.Equal(1, collectCount);

            exporter.CollectionManager.ExitCollect();
            firstCollectExited = true;

            var secondResponse = await secondCollectTask;

            try
            {
                Assert.Equal(2, collectCount);
                Assert.True(firstResponse.GeneratedAtUtc < secondResponse.GeneratedAtUtc);

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
                exporter.CollectionManager.ExitCollect();
            }
        }
        finally
        {
            if (!firstCollectExited)
            {
                exporter.CollectionManager.ExitCollect();
            }
        }
    }

    [Fact]
    public async Task OpenMetricsDoesNotEmitScopeInfoMetricFamily()
    {
        using var meter = new Meter("test_meter", "1.0.0", [new("library.mascot", "dotnetbot")], scope: null);

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource((p) => p.AddAttributes([new("service.name", "prometheus")]))
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener()
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        meter.CreateCounter<int>("counter_1").Add(1);

        var protocol = GetProtocol(openMetricsRequested: true);
        var response = await exporter!.CollectionManager.EnterCollect(protocol);

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
            exporter.CollectionManager.ExitCollect();
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
            .AddPrometheusHttpListener()
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
        var response = await exporter!.CollectionManager.EnterCollect(protocol);

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
            exporter.CollectionManager.ExitCollect();
        }
    }

    [Fact]
    public async Task DuplicateMetricMetadataIsWrittenOncePerScrape()
    {
        using var meter = CreateMeter();

        using var provider = Sdk.CreateMeterProviderBuilder()
            .ConfigureResource((p) => p.AddAttributes([new("service.name", "prometheus")]))
            .AddMeter(meter.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener()
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
        var response = await exporter!.CollectionManager.EnterCollect(protocol);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
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
            .AddPrometheusHttpListener()
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
        var response = await exporter!.CollectionManager.EnterCollect(protocol);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
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
            .AddPrometheusHttpListener()
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
        var response = await exporter!.CollectionManager.EnterCollect(protocol);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
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
            .AddPrometheusHttpListener()
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
        var response = await exporter!.CollectionManager.EnterCollect(protocol);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
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
            .AddPrometheusHttpListener()
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
        var response = await exporter!.CollectionManager.EnterCollect(protocol);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
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
            .AddPrometheusHttpListener()
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
        var response = await exporter!.CollectionManager.EnterCollect(protocol);

        try
        {
            var view = response.View;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            await Verify(output, "txt", PrometheusSerializerTests.VerifySettings);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
        }
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
