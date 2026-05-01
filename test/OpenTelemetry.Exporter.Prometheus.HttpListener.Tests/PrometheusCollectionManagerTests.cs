// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Text;
using System.Text.RegularExpressions;
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

    [Fact]
    public async Task OpenMetricsScopeInfoIsWrittenAsASingleMetricFamily()
    {
        using var meter1 = new Meter("test_meter", "1.0.0");
        using var meter2 = new Meter("test_meter", "2.0.0", [new("library.mascot", "dotnetbot")], scope: null);

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter1.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener()
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        meter1.CreateCounter<int>("counter_1").Add(1);
        meter2.CreateCounter<int>("counter_2").Add(1);

        var response = await exporter!.CollectionManager.EnterCollect(openMetricsRequested: true);
        try
        {
            var output = Encoding.UTF8.GetString(
                response.OpenMetricsView.Array!,
                response.OpenMetricsView.Offset,
                response.OpenMetricsView.Count);

#if NET
            Assert.Equal(1, Regex.Count(output, "^# TYPE otel_scope info$", RegexOptions.Multiline));
            Assert.Equal(1, Regex.Count(output, "^# HELP otel_scope Scope metadata$", RegexOptions.Multiline));
#else
            Assert.Single(Regex.Matches(output, "^# TYPE otel_scope info$", RegexOptions.Multiline));
            Assert.Single(Regex.Matches(output, "^# HELP otel_scope Scope metadata$", RegexOptions.Multiline));
#endif
            Assert.Contains("otel_scope_info{otel_scope_name=\"test_meter\",otel_scope_version=\"1.0.0\"} 1", output, StringComparison.Ordinal);
            Assert.Contains("otel_scope_info{otel_scope_name=\"test_meter\",otel_scope_version=\"2.0.0\",otel_scope_library_mascot=\"dotnetbot\"} 1", output, StringComparison.Ordinal);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
        }
    }

    [Fact]
    public async Task OpenMetricsScopeInfoIsDeduplicatedUsingSerializedScopeLabels()
    {
        using var meter1 = new Meter("test_meter", "1.0.0", [new("library.mascot", true)], scope: null);
        using var meter2 = new Meter("test_meter", "1.0.0", [new("library.mascot", "true")], scope: null);

        using var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter1.Name)
#if PROMETHEUS_HTTP_LISTENER
            .AddPrometheusHttpListener()
#elif PROMETHEUS_ASPNETCORE
            .AddPrometheusExporter()
#endif
            .Build();

#pragma warning disable CA2000 // MeterProvider owns exporter lifecycle
        Assert.True(provider.TryFindExporter(out PrometheusExporter? exporter));
#pragma warning restore CA2000 // MeterProvider owns exporter lifecycle

        meter1.CreateCounter<int>("counter_1").Add(1);
        meter2.CreateCounter<int>("counter_2").Add(1);

        var response = await exporter!.CollectionManager.EnterCollect(openMetricsRequested: true);
        try
        {
            var output = Encoding.UTF8.GetString(
                response.OpenMetricsView.Array!,
                response.OpenMetricsView.Offset,
                response.OpenMetricsView.Count);

#if NET
            Assert.Equal(1, Regex.Count(output, "^otel_scope_info\\{otel_scope_name=\"test_meter\",otel_scope_version=\"1.0.0\",otel_scope_library_mascot=\"true\"\\} 1$", RegexOptions.Multiline));
#else
            Assert.Single(Regex.Matches(output, "^otel_scope_info\\{otel_scope_name=\"test_meter\",otel_scope_version=\"1.0.0\",otel_scope_library_mascot=\"true\"\\} 1$", RegexOptions.Multiline));
#endif
            Assert.Contains("counter_1_total{otel_scope_name=\"test_meter\",otel_scope_version=\"1.0.0\",otel_scope_library_mascot=\"true\"} 1", output, StringComparison.Ordinal);
            Assert.Contains("counter_2_total{otel_scope_name=\"test_meter\",otel_scope_version=\"1.0.0\",otel_scope_library_mascot=\"true\"} 1", output, StringComparison.Ordinal);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
        }
    }

    [Fact]
    public async Task DuplicateMetricMetadataIsWrittenOncePerScrape()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());

        using var provider = Sdk.CreateMeterProviderBuilder()
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

        var response = await exporter!.CollectionManager.EnterCollect(openMetricsRequested: false);
        try
        {
            var view = response.PlainTextView;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            Assert.Single(Regex.Matches(output, "^# TYPE test_metric_bytes_total counter$", RegexOptions.Multiline).Cast<Match>());
            Assert.Single(Regex.Matches(output, "^# UNIT test_metric_bytes_total bytes$", RegexOptions.Multiline).Cast<Match>());
            Assert.Single(Regex.Matches(output, "^# HELP test_metric_bytes_total Test help$", RegexOptions.Multiline).Cast<Match>());
            Assert.Contains("test_metric_bytes_total{otel_scope_name=\"" + meter.Name + "\",source=\"a\"} 1", output, StringComparison.Ordinal);
            Assert.Contains("test_metric_bytes_total{otel_scope_name=\"" + meter.Name + "\",source=\"b\"} 2", output, StringComparison.Ordinal);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
        }
    }

    [Fact]
    public async Task MetricMetadataDiscoveredLaterIsWrittenBeforeSamples()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());

        using var provider = Sdk.CreateMeterProviderBuilder()
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

        var response = await exporter!.CollectionManager.EnterCollect(openMetricsRequested: false);
        try
        {
            var view = response.PlainTextView;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            var typeIndex = output.IndexOf("# TYPE test_metric_total counter", StringComparison.Ordinal);
            var helpIndex = output.IndexOf("# HELP test_metric_total Test help", StringComparison.Ordinal);
            var sampleAIndex = output.IndexOf("test_metric_total{otel_scope_name=\"" + meter.Name + "\",source=\"a\"} 1", StringComparison.Ordinal);
            var sampleBIndex = output.IndexOf("test_metric_total{otel_scope_name=\"" + meter.Name + "\",source=\"b\"} 2", StringComparison.Ordinal);

            Assert.True(typeIndex >= 0, "No TYPE found.");
            Assert.True(helpIndex >= 0, "No HELP found.");
            Assert.True(sampleAIndex >= 0, "No sample A found.");
            Assert.True(sampleBIndex >= 0, "No sample B found.");
            Assert.True(typeIndex < sampleAIndex, "TYPE appears after sample A.");
            Assert.True(typeIndex < sampleBIndex, "TYPE appears after sample B.");
            Assert.True(helpIndex < sampleAIndex, "HELP appears after sample A.");
            Assert.True(helpIndex < sampleBIndex, "HELP appears after sample B.");
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
        }
    }

    [Fact]
    public async Task ConflictingMetricTypesAreDroppedFromAScrape()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());

        using var provider = Sdk.CreateMeterProviderBuilder()
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

        var response = await exporter!.CollectionManager.EnterCollect(openMetricsRequested: true);
        try
        {
            var view = response.OpenMetricsView;
            var output = Encoding.UTF8.GetString(view.Array!, view.Offset, view.Count);

            Assert.DoesNotContain("# TYPE test_metric", output, StringComparison.Ordinal);
            Assert.DoesNotContain("test_metric_total", output, StringComparison.Ordinal);
            Assert.Contains("# EOF", output, StringComparison.Ordinal);
        }
        finally
        {
            exporter.CollectionManager.ExitCollect();
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
