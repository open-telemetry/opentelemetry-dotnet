// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
#if NETFRAMEWORK
using System.Net.Http;
#endif
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

public class PrometheusHttpListenerTests
{
    private const string MeterVersion = "1.0.1";

    private const string UriPrefixesObsoleteMessage = "Tests the obsolete UriPrefixes property. Remove when UriPrefixes is removed.";

    private static readonly string MeterName = Utils.GetCurrentMethodName();

    [Theory]
    [InlineData("http://+:9464")]
    [InlineData("http://*:9464")]
    [InlineData("http://+:9464/")]
    [InlineData("http://*:9464/")]
    [InlineData("https://example.com")]
    [InlineData("http://127.0.0.1")]
    [InlineData("http://example.com", "https://example.com", "http://127.0.0.1")]
    [InlineData("http://example.com")]
    [Obsolete(UriPrefixesObsoleteMessage)]
    public void UriPrefixesPositiveTest(params string[] uriPrefixes)
        => TestPrometheusHttpListenerUriPrefixOptions(uriPrefixes);

    [Fact]
    [Obsolete(UriPrefixesObsoleteMessage)]
    public void UriPrefixesNull() =>
        Assert.Throws<ArgumentNullException>(() =>
        {
            TestPrometheusHttpListenerUriPrefixOptions(null!);
        });

    [Fact]
    [Obsolete(UriPrefixesObsoleteMessage)]
    public void UriPrefixesEmptyList() =>
        Assert.Throws<ArgumentException>(() =>
        {
            TestPrometheusHttpListenerUriPrefixOptions([]);
        });

    [Fact]
    [Obsolete(UriPrefixesObsoleteMessage)]
    public void UriPrefixesInvalid() =>
        Assert.Throws<ArgumentException>(() =>
        {
            TestPrometheusHttpListenerUriPrefixOptions(["ftp://example.com"]);
        });

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration()
        => await RunPrometheusExporterHttpServerIntegrationTest();

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_NoMetrics()
        => await RunPrometheusExporterHttpServerIntegrationTest(skipMetrics: true);

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_NoOpenMetrics()
        => await RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: string.Empty);

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_UseOpenMetricsVersionHeader()
        => await RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: "application/openmetrics-text; version=1.0.0");

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_NoOpenMetrics_WithMeterTags()
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("meter1", "value1"),
            new("meter2", "value2"),
        };

        await RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: string.Empty, meterTags: tags);
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_OpenMetrics_WithMeterTags()
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("meter1", "value1"),
            new("meter2", "value2"),
        };

        await RunPrometheusExporterHttpServerIntegrationTest(acceptHeader: "application/openmetrics-text; version=1.0.0", meterTags: tags);
    }

    [Fact]
    public void PrometheusHttpListenerThrowsOnStart()
    {
        var random = new Random();
        var retryAttempts = 5;
        int boundPort = 0;

        PrometheusExporter? exporter = null;
        PrometheusHttpListener? listener = null;

        // Step 1: Start a listener on a random port.
        while (retryAttempts-- != 0)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            var port = random.Next(2000, 5000);
#pragma warning restore CA5394 // Do not use insecure randomness

            try
            {
                exporter = new PrometheusExporter(new());
                listener = new PrometheusHttpListener(
                    exporter,
                    new()
                    {
                        Host = "localhost",
                        Port = port,
                    });

                listener.Start();
                boundPort = port;

                break;
            }
            catch
            {
                // ignored
            }
        }

        if (retryAttempts == 0)
        {
            throw new InvalidOperationException("PrometheusHttpListener could not be started");
        }

        // Step 2: Make sure if we start a second listener on the same port an exception is thrown.
        Assert.Throws<HttpListenerException>(() =>
        {
            using var exporter = new PrometheusExporter(new());
            using var listener = new PrometheusHttpListener(
                exporter,
                new()
                {
                    Host = "localhost",
                    Port = boundPort,
                });

            listener.Start();
        });

        exporter?.Dispose();
        listener?.Dispose();
    }

    [Fact]
    public async Task PrometheusHttpListenerStopDoesNotWaitForInFlightRequest()
    {
        var timeout = TimeSpan.FromSeconds(5);

        using var exporter = new PrometheusExporter(new());
        using var collectStarted = new ManualResetEventSlim();
        using var allowCollectToComplete = new ManualResetEventSlim();
        using var listener = StartPrometheusHttpListener(exporter, out var address);
        using var client = new HttpClient();

        exporter.Collect = _ =>
        {
            collectStarted.Set();

            return
                allowCollectToComplete.Wait(timeout) ?
                true :
                throw new TimeoutException("Timed out waiting for the test to release the scrape.");
        };

        var requestTask = client.GetAsync(new Uri($"{address}metrics"));

        Assert.True(collectStarted.Wait(timeout));

        var stopTask = Task.Run(listener.Stop);

        try
        {
            using var cts = new CancellationTokenSource(timeout);
            var completedTask = await Task.WhenAny(stopTask, Task.Delay(timeout, cts.Token));

            Assert.Same(stopTask, completedTask);

            await stopTask;
        }
        finally
        {
            allowCollectToComplete.Set();

            try
            {
                using var response = await requestTask;
            }
            catch (HttpRequestException)
            {
            }
            catch (TaskCanceledException)
            {
            }
        }
    }

    [Fact]
    public async Task PrometheusHttpListenerHandlesConcurrentScrapes()
    {
        var timeout = TimeSpan.FromSeconds(5);

        using var exporter = new PrometheusExporter(new()
        {
            ScrapeResponseCacheDurationMilliseconds = 0,
        });
        using var firstCollectStarted = new ManualResetEventSlim();
        using var allowFirstCollectToComplete = new ManualResetEventSlim();
        using var secondCollectStarted = new ManualResetEventSlim();
        using var allowSecondCollectToComplete = new ManualResetEventSlim();
        using var listener = StartPrometheusHttpListener(exporter, out var address);
        using var client = new HttpClient();
        var collectCount = 0;

        exporter.Collect = _ =>
        {
            var currentCollect = Interlocked.Increment(ref collectCount);

            if (currentCollect == 1)
            {
                firstCollectStarted.Set();

                if (!allowFirstCollectToComplete.Wait(timeout))
                {
                    throw new TimeoutException("Timed out waiting for the test to release the first scrape.");
                }
            }
            else if (currentCollect == 2)
            {
                secondCollectStarted.Set();

                if (!allowSecondCollectToComplete.Wait(timeout))
                {
                    throw new TimeoutException("Timed out waiting for the test to release the second scrape.");
                }
            }

            return true;
        };

        var firstRequestTask = client.GetAsync(new Uri($"{address}metrics"));

        Assert.True(firstCollectStarted.Wait(timeout));

        var secondRequestTask = client.GetAsync(new Uri($"{address}metrics"));

        await Task.Delay(100);

        allowFirstCollectToComplete.Set();

        try
        {
            using var firstResponse = await firstRequestTask;

            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

#if NET
            await secondRequestTask.WaitAsync(timeout);
#else
            using var cts = new CancellationTokenSource(timeout);
            var completedTask = await Task.WhenAny(secondRequestTask, Task.Delay(timeout, cts.Token));
            Assert.Same(secondRequestTask, completedTask);
#endif

            Assert.False(secondCollectStarted.IsSet);

            using var secondResponse = await secondRequestTask;

            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
            Assert.Equal(1, Volatile.Read(ref collectCount));
        }
        finally
        {
            allowFirstCollectToComplete.Set();
            allowSecondCollectToComplete.Set();
        }
    }

    [Theory]
    [InlineData("application/openmetrics-text")]
    [InlineData("")]
    public async Task PrometheusExporterHttpServerIntegration_TestBufferSizeIncrease_With_LargePayload(string acceptHeader)
    {
        using var meter = new Meter(MeterName, MeterVersion);

        var attributes = new List<KeyValuePair<string, object>>();
        var oneKb = new string('A', 1024);
        for (var x = 0; x < 8500; x++)
        {
            attributes.Add(new KeyValuePair<string, object>(x.ToString(CultureInfo.InvariantCulture), oneKb));
        }

        var provider = BuildMeterProvider(meter, attributes, out var address);

        for (var x = 0; x < 1000; x++)
        {
            var counter = meter.CreateCounter<double>("counter_double_" + x, unit: "By");
            counter.Add(1);
        }

        using var client = new HttpClient();

        if (!string.IsNullOrEmpty(acceptHeader))
        {
            client.DefaultRequestHeaders.Add("Accept", acceptHeader);
        }

        using var response = await client.GetAsync(new Uri($"{address}metrics"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("counter_double_999", content, StringComparison.Ordinal);
        Assert.DoesNotContain('\0', content);

        provider.Dispose();
    }

    [Fact]
    public async Task HostAndPort_Used_When_UriPrefixesNotSet()
    {
        using var meter = new Meter(MeterName, MeterVersion);

        var random = new Random();
#pragma warning disable CA5394 // Do not use insecure randomness
        var port = random.Next(2000, 5000);
#pragma warning restore CA5394 // Do not use insecure randomness

        var provider = BuildMeterProvider(
            meter,
            [],
            o =>
            {
                o.Host = "localhost";
                o.Port = port;
            },
            out var address);

        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri($"{address}metrics"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        provider.Dispose();
    }

    [Fact]
    public async Task PortOnly_Set_HostDefaultsToLocalhost()
    {
        using var meter = new Meter(MeterName, MeterVersion);

        var random = new Random();
#pragma warning disable CA5394 // Do not use insecure randomness
        var port = random.Next(2000, 5000);
#pragma warning restore CA5394 // Do not use insecure randomness

        var provider = BuildMeterProvider(meter, [], o => o.Port = port, out var address);

        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri($"{address}metrics"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        provider.Dispose();
    }

    [Fact]
    public async Task HostOnly_Set_PortDefaultsTo9464()
    {
        using var meter = new Meter(MeterName, MeterVersion);

        MeterProvider provider;
        string address;
        try
        {
            provider = BuildMeterProvider(meter, [], o => o.Host = "localhost", out address);
        }
        catch (HttpListenerException)
        {
            // Default port 9464 is not available on this machine; skip.
            return;
        }

        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri($"{address}metrics"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        provider.Dispose();
    }

    [Fact]
    [Obsolete(UriPrefixesObsoleteMessage)]
    public async Task ExplicitUriPrefixes_TakePrecedence_Over_HostPort()
    {
        var random = new Random();
        var retryAttempts = 5;
        string? explicitPrefix = null;
        MeterProvider? provider = null;

        while (retryAttempts-- != 0)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            var port = random.Next(2000, 5000);
#pragma warning restore CA5394 // Do not use insecure randomness
            explicitPrefix = new UriBuilder(Uri.UriSchemeHttp, "localhost", port).Uri.AbsoluteUri;

            try
            {
                var prefix = explicitPrefix;
                provider = BuildMeterProvider(
                    new Meter(MeterName, MeterVersion),
                    [],
                    o =>
                    {
                        o.Host = "127.0.0.1";
                        o.Port = 9999;
                        o.UriPrefixes = [prefix];
                    },
                    out _);
                break;
            }
            catch
            {
                // try another port
            }
        }

        if (provider == null)
        {
            throw new InvalidOperationException("PrometheusHttpListener could not be started using explicit UriPrefixes");
        }

        using var client = new HttpClient();
        using var response = await client.GetAsync(new Uri($"{explicitPrefix}metrics"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        provider.Dispose();
    }

    [Fact]
    public void Host_DefaultValue_IsLocalhost()
        => Assert.Equal("localhost", new PrometheusHttpListenerOptions().Host);

    [Fact]
    public void Port_DefaultValue_Is9464()
        => Assert.Equal(9464, new PrometheusHttpListenerOptions().Port);

    [Obsolete("Supports tests for the obsolete UriPrefixes property. Remove when UriPrefixes is removed.")]
    private static void TestPrometheusHttpListenerUriPrefixOptions(string[] uriPrefixes)
    {
        using var exporter = new PrometheusExporter(new());
        using var listener = new PrometheusHttpListener(
            exporter,
            new()
            {
                UriPrefixes = uriPrefixes,
            });
    }

    private static PrometheusHttpListener StartPrometheusHttpListener(PrometheusExporter exporter, out string address)
    {
#if NET
        var random = Random.Shared;
#else
        var random = new Random();
#endif

        var retryAttempts = 5;

        while (retryAttempts-- != 0)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            var port = random.Next(2000, 5000);
#pragma warning restore CA5394 // Do not use insecure randomness

            string generatedAddress = new UriBuilder(Uri.UriSchemeHttp, "localhost", port).Uri.ToString();

            var listener = new PrometheusHttpListener(
                exporter,
                new() { Port = port });

            try
            {
                listener.Start();

                address = generatedAddress;
                return listener;
            }
            catch (HttpListenerException)
            {
                listener.Dispose();
            }
        }

        throw new InvalidOperationException($"{nameof(PrometheusHttpListener)} could not be started.");
    }

    private static MeterProvider BuildMeterProvider(Meter meter, IEnumerable<KeyValuePair<string, object>> attributes, out string address)
    {
        var random = new Random();
        var retryAttempts = 5;
        string? generatedAddress = null;
        MeterProvider? provider = null;

        while (retryAttempts-- != 0)
        {
#pragma warning disable CA5394 // Do not use insecure randomness
            var port = random.Next(2000, 5000);
#pragma warning restore CA5394 // Do not use insecure randomness

            var uriBuilder = new UriBuilder(Uri.UriSchemeHttp, "localhost", port);
            generatedAddress = uriBuilder.Uri.AbsoluteUri;

            try
            {
                provider = Sdk.CreateMeterProviderBuilder()
                    .AddMeter(meter.Name)
                    .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1").AddAttributes(attributes))
                    .AddPrometheusHttpListener(options =>
                    {
                        options.Host = "localhost";
                        options.Port = port;
                    })
                    .Build();

                break;
            }
            catch
            {
                // ignored
            }
        }

        address = generatedAddress!;

        return provider ?? throw new InvalidOperationException("HttpListener could not be started");
    }

    private static MeterProvider BuildMeterProvider(Meter meter, IEnumerable<KeyValuePair<string, object>> attributes, Action<PrometheusHttpListenerOptions> configureOptions, out string address)
    {
        string? capturedHost = null;
        int capturedPort = 0;

        var provider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .ConfigureResource(x => x.Clear().AddService("my_service", serviceInstanceId: "id1").AddAttributes(attributes))
            .AddPrometheusHttpListener(options =>
            {
                configureOptions(options);
                capturedHost = options.Host;
                capturedPort = options.Port;
            })
            .Build();

        address = new UriBuilder(Uri.UriSchemeHttp, capturedHost!, capturedPort).Uri.AbsoluteUri;

        return provider;
    }

    private static async Task RunPrometheusExporterHttpServerIntegrationTest(bool skipMetrics = false, string acceptHeader = "application/openmetrics-text", KeyValuePair<string, object?>[]? meterTags = null)
    {
        var requestOpenMetrics = acceptHeader.StartsWith("application/openmetrics-text", StringComparison.Ordinal);

        using var meter = new Meter(MeterName, MeterVersion, meterTags);

        var provider = BuildMeterProvider(meter, [], out var address);

        var counterTags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value1"),
            new("key2", "value2"),
        };

        var counter = meter.CreateCounter<double>("counter_double", unit: "By");
        if (!skipMetrics)
        {
            counter.Add(100.18D, counterTags);
            counter.Add(0.99D, counterTags);
        }

        using var client = new HttpClient();

        if (!string.IsNullOrEmpty(acceptHeader))
        {
            client.DefaultRequestHeaders.Add("Accept", acceptHeader);
        }

        using var response = await client.GetAsync(new Uri($"{address}metrics"));

        if (!skipMetrics)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Content.Headers.Contains("Last-Modified"));

            if (requestOpenMetrics)
            {
                Assert.Equal("application/openmetrics-text; version=1.0.0; charset=utf-8", response.Content.Headers.ContentType?.ToString());
            }
            else
            {
                Assert.Equal("text/plain; charset=utf-8; version=0.0.4", response.Content.Headers.ContentType?.ToString());
            }

            var additionalTags = meterTags is { Length: > 0 }
                ? $"{string.Join(",", meterTags.Select(x => $"{x.Key}='{x.Value}'"))},"
                : string.Empty;

            var content = await response.Content.ReadAsStringAsync();

            var expected = requestOpenMetrics
                ? "# TYPE target info\n"
                  + "# HELP target Target metadata\n"
                  + "target_info{service_name='my_service',service_instance_id='id1'} 1\n"
                  + "# TYPE otel_scope_info info\n"
                  + "# HELP otel_scope_info Scope metadata\n"
                  + $"otel_scope_info{{otel_scope_name='{MeterName}'}} 1\n"
                  + "# TYPE counter_double_bytes counter\n"
                  + "# UNIT counter_double_bytes bytes\n"
                  + $"counter_double_bytes_total{{otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',{additionalTags}key1='value1',key2='value2'}} 101.17\n"
                  + "# EOF\n"
                : "# TYPE counter_double_bytes_total counter\n"
                  + "# UNIT counter_double_bytes_total bytes\n"
                  + $"counter_double_bytes_total{{otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',{additionalTags}key1='value1',key2='value2'}} 101.17\n"
                  + "# EOF\n";

            Assert.Matches(("^" + expected + "$").Replace('\'', '"'), content);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        provider.Dispose();
    }
}
