// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
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

    private const string UriPrefixesObsoleteMessage = "Tests the obsolete UriPrefixes property.";

    private static readonly string MeterName = Utils.GetCurrentMethodName();

    private static readonly ConcurrentDictionary<int, int> ConsumedPorts = [];

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
    {
        var options = TestPrometheusHttpListenerUriPrefixOptions(uriPrefixes);
        Assert.Equivalent(uriPrefixes, options.UriPrefixes);
    }

    [Fact]
    [Obsolete(UriPrefixesObsoleteMessage)]
    public void UriPrefixesNull() =>
        Assert.Throws<ArgumentNullException>(() => TestPrometheusHttpListenerUriPrefixOptions(null!));

    [Fact]
    [Obsolete(UriPrefixesObsoleteMessage)]
    public void UriPrefixesEmptyList() =>
        Assert.Throws<ArgumentException>(() => TestPrometheusHttpListenerUriPrefixOptions([]));

    [Fact]
    [Obsolete(UriPrefixesObsoleteMessage)]
    public void UriPrefixesInvalid() =>
        Assert.Throws<ArgumentException>(() => TestPrometheusHttpListenerUriPrefixOptions(["ftp://example.com"]));

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

        await RunPrometheusExporterHttpServerIntegrationTest(
            acceptHeader: string.Empty,
            meterTags: tags);
    }

    [Fact]
    public async Task PrometheusExporterHttpServerIntegration_OpenMetrics_WithMeterTags()
    {
        var tags = new KeyValuePair<string, object?>[]
        {
            new("meter1", "value1"),
            new("meter2", "value2"),
        };

        await RunPrometheusExporterHttpServerIntegrationTest(
            acceptHeader: "application/openmetrics-text; version=1.0.0",
            meterTags: tags);
    }

    [Fact]
    public void PrometheusHttpListenerThrowsOnStart()
    {
        // Step 1: Start a listener on a random port.
        using var context = CreateListener();

        // Step 2: Try to start a second listener on the same port
        using var exporter = new PrometheusExporter(new());
        using var listener = new PrometheusHttpListener(
            exporter,
            new()
            {
                Host = "localhost",
                Port = context.Port,
            });

        Assert.Throws<HttpListenerException>(() => listener.Start());
    }

    [Theory]
    [InlineData("application/openmetrics-text")]
    [InlineData("")]
    public async Task PrometheusExporterHttpServerIntegration_TestBufferSizeIncrease_With_LargePayload(string acceptHeader)
    {
        using var meter = new Meter(MeterName, MeterVersion);

        var attributes = new List<KeyValuePair<string, object>>();
        var oneKb = new string('A', 1024);

        for (var x = 0; x < 8_500; x++)
        {
            attributes.Add(new KeyValuePair<string, object>(x.ToString(CultureInfo.InvariantCulture), oneKb));
        }

        using var context = CreateMeterProvider(meter, attributes: attributes);

        for (var x = 0; x < 1_000; x++)
        {
            var counter = meter.CreateCounter<double>("counter_double_" + x, unit: "By");
            counter.Add(1);
        }

        using var client = new HttpClient()
        {
            BaseAddress = context.BaseAddress,
        };

        if (!string.IsNullOrEmpty(acceptHeader))
        {
            client.DefaultRequestHeaders.Add("Accept", acceptHeader);
        }

        using var response = await client.GetAsync(new Uri("metrics", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();

        Assert.Contains("counter_double_999", content, StringComparison.Ordinal);
        Assert.DoesNotContain('\0', content);
    }

    [Fact]
    public async Task HostAndPort_Used_When_UriPrefixesNotSet()
    {
        using var meter = new Meter(MeterName, MeterVersion);

        var host = "localhost";
        var port = GetRandomPort();

        using var context = CreateMeterProvider(meter, configure: (options) =>
        {
            options.Host = host;
            options.Port = port;

            return port;
        });

        Assert.Equal(host, context.BaseAddress.Host);
        Assert.Equal(port, context.Port);

        using var client = new HttpClient() { BaseAddress = context.BaseAddress };
        using var response = await client.GetAsync(new Uri("metrics", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PortOnly_Set_HostDefaultsToLocalhost()
    {
        using var meter = new Meter(MeterName, MeterVersion);

        var port = GetRandomPort();

        using var context = CreateMeterProvider(meter, configure: (options) =>
        {
            options.Port = port;
            return port;
        });

        Assert.Equal("localhost", context.BaseAddress.Host);
        Assert.Equal(port, context.Port);

        using var client = new HttpClient() { BaseAddress = context.BaseAddress };
        using var response = await client.GetAsync(new Uri("metrics", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HostOnly_Set_Port_DefaultsTo9464()
    {
#if NET
        if (OperatingSystem.IsLinux())
        {
            // Linux does not like binding to 127.0.0.1 for some reason
            return;
        }
#endif

        using var meter = new Meter(MeterName, MeterVersion);

        var host = "127.0.0.1";

        using var context = CreateMeterProvider(meter, configure: (options) =>
        {
            options.Host = host;
            return options.Port;
        });

        Assert.Equal(9464, context.Port);

        using var client = new HttpClient() { BaseAddress = context.BaseAddress };
        using var response = await client.GetAsync(new Uri("metrics", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Obsolete(UriPrefixesObsoleteMessage)]
    public async Task ExplicitUriPrefixes_TakePrecedence_Over_HostPort()
    {
        using var meter = new Meter(MeterName, MeterVersion);

        int port = 0;

        using var context = CreateMeterProvider(meter, configure: (options) =>
        {
            options.Host = "prometheus.local";
            options.Port = 9999;

            port = GetRandomPort();

            options.UriPrefixes = [$"http://localhost:{port}"];

            return port;
        });

        Assert.Equal(port, context.Port);
        Assert.Equal($"http://localhost:{port}/", context.BaseAddress.ToString());

        using var client = new HttpClient() { BaseAddress = context.BaseAddress };
        using var response = await client.GetAsync(new Uri("metrics", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void Host_DefaultValue_Is_Localhost()
        => Assert.Equal("localhost", new PrometheusHttpListenerOptions().Host);

    [Fact]
    public void Port_DefaultValue_Is_9464()
        => Assert.Equal(9464, new PrometheusHttpListenerOptions().Port);

    private static async Task RunPrometheusExporterHttpServerIntegrationTest(
        bool skipMetrics = false,
        string acceptHeader = "application/openmetrics-text",
        KeyValuePair<string, object?>[]? meterTags = null)
    {
        var requestOpenMetrics = acceptHeader.StartsWith("application/openmetrics-text", StringComparison.Ordinal);

        using var meter = new Meter(MeterName, MeterVersion, meterTags);

        using var context = CreateMeterProvider(meter);

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

        using var client = new HttpClient()
        {
            BaseAddress = context.BaseAddress,
        };

        if (!string.IsNullOrEmpty(acceptHeader))
        {
            client.DefaultRequestHeaders.Add("Accept", acceptHeader);
        }

        using var response = await client.GetAsync(new Uri("metrics", UriKind.Relative));

        if (!skipMetrics)
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.True(response.Content.Headers.Contains("Last-Modified"));

            if (requestOpenMetrics)
            {
                Assert.Equal("application/openmetrics-text; version=1.0.0; charset=utf-8; escaping=underscores", response.Content.Headers.ContentType?.ToString());
            }
            else
            {
                Assert.Equal("text/plain; charset=utf-8; version=0.0.4", response.Content.Headers.ContentType?.ToString());
            }

            var additionalTags = meterTags is { Length: > 0 }
                ? $"{string.Join(",", meterTags.Select(x => $"{x.Key}='{x.Value}'"))},"
                : string.Empty;
            var createdMetricSample = requestOpenMetrics
                ? $"counter_double_bytes_created{{otel_scope_name='{MeterName}',otel_scope_version='{MeterVersion}',{additionalTags}key1='value1',key2='value2'}} [0-9]+(?:\\.[0-9]+)?\n"
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
                  + createdMetricSample
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
    }

    private static int GetRandomPort()
    {
        int port;

        // Try to only use each port number exactly once
        while (!ConsumedPorts.TryAdd(port = TcpPortProvider.GetOpenPort(), port))
        {
        }

        return port;
    }

    private static PrometheusTestContext CreateListener(int? port = null)
    {
        var maximumAttempts = 5;
        var attemptsLeft = maximumAttempts;
        int boundPort = 0;

        var options = new PrometheusHttpListenerOptions()
        {
            Host = "localhost",
        };

#pragma warning disable CA2000 // Dispose objects before losing scope
        var exporter = new PrometheusExporter(new());
#pragma warning restore CA2000 // Dispose objects before losing scope

        try
        {
            while (attemptsLeft-- > 0)
            {
                port ??= GetRandomPort();

                options.Port = port.Value;

#pragma warning disable CA2000 // Dispose objects before losing scope
                var listener = new PrometheusHttpListener(exporter, options);
#pragma warning restore CA2000 // Dispose objects before losing scope

                try
                {
                    listener.Start();

                    return new(exporter, listener, boundPort);
                }
                catch (Exception)
                {
                    // Try again, possibly with a different port
                    listener.Dispose();
                }
            }

            throw new InvalidOperationException($"{nameof(PrometheusHttpListener)} could not be started within {maximumAttempts} attempts.");
        }
        catch (Exception)
        {
            exporter.Dispose();
            throw;
        }
    }

    private static MeterProviderTestContext CreateMeterProvider(
        Meter meter,
        Func<PrometheusHttpListenerOptions, int>? configure = null,
        IEnumerable<KeyValuePair<string, object>>? attributes = null)
    {
        var maximumAttempts = 5;
        var attemptsLeft = maximumAttempts;

        configure ??= static (options) =>
        {
            options.Port = GetRandomPort();
            return options.Port;
        };

        while (attemptsLeft-- > 0)
        {
            int port = -1;

            var provider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .ConfigureResource((p) =>
                {
                    p.Clear().AddService("my_service", serviceInstanceId: "id1");

                    if (attributes is not null)
                    {
                        p.AddAttributes(attributes);
                    }
                })
                .AddPrometheusHttpListener((options) =>
                {
                    port = configure(options);
                })
                .Build();

            return new(provider, port);
        }

        throw new InvalidOperationException($"{nameof(MeterProvider)} could not be created within {maximumAttempts} attempts.");
    }

    [Obsolete("Supports tests for the obsolete UriPrefixes property.")]
    private static PrometheusHttpListenerOptions TestPrometheusHttpListenerUriPrefixOptions(string[] uriPrefixes)
    {
        var options = new PrometheusHttpListenerOptions()
        {
            UriPrefixes = uriPrefixes,
        };

        using var exporter = new PrometheusExporter(new());
        using var listener = new PrometheusHttpListener(
            exporter,
            options);

        return options;
    }

    private sealed class MeterProviderTestContext(MeterProvider provider, int port) : IDisposable
    {
        public MeterProvider Provider { get; } = provider;

        public Uri BaseAddress { get; } = new UriBuilder(Uri.UriSchemeHttp, "localhost", port).Uri;

        public int Port { get; } = port;

        public void Dispose()
            => this.Provider.Dispose();
    }

    private sealed class PrometheusTestContext(PrometheusExporter exporter, PrometheusHttpListener listener, int port) : IDisposable
    {
        public PrometheusExporter Exporter { get; } = exporter;

        public PrometheusHttpListener Listener { get; } = listener;

        public int Port { get; } = port;

        public void Dispose()
        {
            this.Exporter.Dispose();
            this.Listener.Dispose();
        }
    }
}
