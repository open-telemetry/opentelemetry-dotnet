// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Exporter.Prometheus.Tests;

[Collection(GcAllocationsCollectionDefinition.Name)]
public class PrometheusHttpListenerAllocationTests
{
    private const string MeterVersion = "1.0.1";
    private const string MeterName = nameof(PrometheusHttpListenerAllocationTests);

    [Fact]
    public async Task ScrapeDoesNotAllocateACopyOfTheResponseBody()
    {
        using var meter = new Meter(MeterName, MeterVersion);

        using var context = PrometheusHttpListenerTests.CreateMeterProvider(
            meter,
            configureListener: options =>
            {
                // Serve every scrape from the cached response so measuring one does not include the collection
                options.ScrapeResponseCacheDurationMilliseconds = 60_000;
                return options.Port;
            });

        // Roughly 12 MB of exposition text, far larger than a single response write
        var padding = new string('x', 24_576);
        for (var i = 0; i < 500; i++)
        {
            var counter = meter.CreateCounter<long>("counter_long_" + i.ToString(CultureInfo.InvariantCulture));
            counter.Add(1, new KeyValuePair<string, object?>("key", padding));
        }

        using var client = new HttpClient { BaseAddress = context.BaseAddress };
        var buffer = new byte[PrometheusExporter.MaximumResponseWriteSize];

        // Warm up the collection, the response cache and the connection
        var expectedLength = await ScrapeAsync();

        Assert.True(expectedLength > padding.Length * 400, $"Expected a large response, but it was {expectedLength} bytes.");

        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);

        var actualLength = await ScrapeAsync();

        var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;

        Assert.Equal(expectedLength, actualLength);

        // The response is written from a buffer shared by all scrapes. The managed (non-Windows)
        // HttpListener copies the content of the first write into a new buffer with the response
        // headers, so writing it in one call allocates (and, for a client that does not read the
        // response, retains) a copy of the entire body per scrape.
        Assert.True(
            allocated < expectedLength / 4,
            $"Expected serving a cached {expectedLength} byte response not to allocate a copy of it, but {allocated} bytes were allocated.");

        async Task<long> ScrapeAsync()
        {
            using var response = await client.GetAsync(
                new Uri("metrics", UriKind.Relative),
                HttpCompletionOption.ResponseHeadersRead,
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            using var stream = await response.Content.ReadAsStreamAsync(TestContext.Current.CancellationToken);

            long length = 0;
            int read;

            while ((read = await stream.ReadAsync(buffer, TestContext.Current.CancellationToken)) > 0)
            {
                length += read;
            }

            return length;
        }
    }
}
#endif
