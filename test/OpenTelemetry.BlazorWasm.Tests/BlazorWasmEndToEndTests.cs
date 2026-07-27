// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;
using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;

namespace OpenTelemetry.BlazorWasm.Tests;

public sealed class BlazorWasmEndToEndTests : IClassFixture<BlazorWasmAppFixture>
{
    // Contract shared with OpenTelemetry.BlazorWasm.TestApp (kept in sync by hand
    // to avoid a project reference that would require the wasm workload to build).
    private const string ServiceName = "otel-blazor-wasm-testapp";
    private const string ActivityName = "BlazorWasmScenario";
    private const string ActivityTagKey = "otel.blazor.scenario";
    private const string ActivityTagValue = "end-to-end";
    private const string CounterName = "blazor.wasm.scenario.count";
    private const string HistogramName = "blazor.wasm.scenario.duration";
    private const string HttpClientSourceName = "System.Net.Http";
    private const string HttpClientDurationMetric = "http.client.request.duration";

    private const int TestTimeoutMilliseconds = 90_000;

    // Number of cold-boot attempts. The Blazor client fetches many framework
    // assets on first load, and a transient browser network error can abort
    // those downloads and leave the app stuck loading, so some retries are allowed.
    private const int StartupAttempts = 3;

    private static readonly TimeSpan CollectTimeout = TimeSpan.FromSeconds(60);

    private readonly BlazorWasmAppFixture wasmFixture;
    private readonly BrowserFixture browserFixture;

    public BlazorWasmEndToEndTests(BlazorWasmAppFixture fixture, ITestOutputHelper outputHelper)
    {
        this.wasmFixture = fixture;
        this.browserFixture = new BrowserFixture(outputHelper);
    }

    [Fact(Timeout = TestTimeoutMilliseconds)]
    public async Task LogsAreExported()
    {
        // Arrange
        var collector = this.wasmFixture.Collector;

        await this.browserFixture.WithPageAsync(async page =>
        {
            await NavigateAndWaitForStartupAsync(page, collector.BaseUrl);

            // Act
            await page.Locator("#increment-counter").ClickAsync();

            // Assert
            await Assertions.Expect(page.Locator("#counter-value")).ToHaveTextAsync("1", new() { Timeout = 10_000 });

            AssertNoAppErrors(await GetErrorAsync(page));

            await WaitForAsync(
                () => HasLog(collector, "Blazor WASM end-to-end") && HasLog(collector, "Counter button clicked"),
                () => Detail(collector));

            Assert.True(HasServiceName(collector), $"Expected resource attribute service.name='{ServiceName}'.");

            var scenarioLog = GetLogRecords(collector).First(l => Body(l).Contains("Blazor WASM end-to-end", StringComparison.Ordinal));

            Assert.Equal((int)SeverityNumber.Info, (int)scenarioLog.SeverityNumber);
        });
    }

    [Fact(Timeout = TestTimeoutMilliseconds)]
    public async Task MetricsAreExported()
    {
        // Arrange
        var collector = this.wasmFixture.Collector;

        await this.browserFixture.WithPageAsync(async page =>
        {
            await NavigateAndWaitForStartupAsync(page, collector.BaseUrl);

            // Act
            await page.Locator("#increment-counter").ClickAsync();

            // Assert
            AssertNoAppErrors(await GetErrorAsync(page));

            await WaitForAsync(
                () => HasMetric(collector, CounterName) && HasMetric(collector, HistogramName),
                () => Detail(collector));

            Assert.True(HasServiceName(collector), $"Expected resource attribute service.name='{ServiceName}'.");
        });
    }

    [Fact(Timeout = TestTimeoutMilliseconds)]
    public async Task TracesAreExported()
    {
        // Arrange
        var collector = this.wasmFixture.Collector;

        await this.browserFixture.WithPageAsync(async (page) =>
        {
            // Act
            await NavigateAndWaitForStartupAsync(page, collector.BaseUrl);

            // Assert
            AssertNoAppErrors(await GetErrorAsync(page));

            await WaitForAsync(() => HasScenarioTrace(collector), () => Detail(collector));

            Assert.True(HasServiceName(collector), $"Expected resource attribute service.name='{ServiceName}'.");
        });
    }

    [Fact(Timeout = TestTimeoutMilliseconds)]
    public async Task HttpClientInstrumentationIsExported()
    {
        // Arrange
        var collector = this.wasmFixture.Collector;

        await this.browserFixture.WithPageAsync(async page =>
        {
            await NavigateAndWaitForStartupAsync(page, collector.BaseUrl);

            // Act
            await page.Locator("#call-http").ClickAsync();

            // Assert
            await Assertions.Expect(page.Locator("#http-done")).ToHaveTextAsync("True", new() { Timeout = 30_000 });
            await Assertions.Expect(page.Locator("#http-status")).ToHaveTextAsync("200", new() { Timeout = 10_000 });

            AssertNoAppErrors(await GetErrorAsync(page));

            await WaitForAsync(
                () => HasHttpClientSpan(collector) && HasMetric(collector, HttpClientDurationMetric),
                () => Detail(collector));

            Assert.True(HasServiceName(collector), $"Expected resource attribute service.name='{ServiceName}'.");
        });
    }

    private static async Task NavigateAndWaitForStartupAsync(IPage page, string url)
    {
        for (var attempt = 0; attempt < StartupAttempts; attempt++)
        {
            try
            {
                await page.GotoAsync(url, new PageGotoOptions { WaitUntil = WaitUntilState.NetworkIdle });
                await Assertions.Expect(page.Locator("#status")).ToHaveTextAsync("completed", new() { Timeout = 30_000 });
                return;
            }
            catch (PlaywrightException) when (attempt < StartupAttempts)
            {
                // A transient network error (e.g. ERR_NETWORK_CHANGED) can abort the
                // WASM asset downloads on a cold boot, leaving the app stuck loading.
                // Reload and try again.
                await Task.Delay(1_000);
            }
        }

        throw new InvalidOperationException("Failed to navigate to the page after multiple attempts.");
    }

    private static async Task<string> GetErrorAsync(IPage page)
        => await page.Locator("#error").InnerTextAsync();

    private static void AssertNoAppErrors(string error)
        => Assert.True(string.IsNullOrWhiteSpace(error), $"The app reported an error:{Environment.NewLine}{error}");

    private static async Task WaitForAsync(Func<bool> condition, Func<string> failureDetail)
    {
        var deadline = DateTime.UtcNow + CollectTimeout;

        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(500);
        }

        Assert.True(condition(), "Timed out waiting for telemetry to be exported. " + failureDetail());
    }

    private static string Body(LogRecord logRecord) => logRecord.Body?.StringValue ?? string.Empty;

    private static bool HasScenarioTrace(OtlpHttpCollector collector) =>
        GetSpans(collector)
            .Any((p) => p.Span.Name == ActivityName &&
                        p.Span.Attributes.Any((r) => r.Key == ActivityTagKey && r.Value?.StringValue == ActivityTagValue));

    private static bool HasHttpClientSpan(OtlpHttpCollector collector) =>
        GetSpans(collector)
            .Any((p) => p.Scope == HttpClientSourceName);

    private static bool HasLog(OtlpHttpCollector collector, string bodyContains) =>
        GetLogRecords(collector)
            .Any((p) => Body(p).Contains(bodyContains, StringComparison.Ordinal));

    private static bool HasMetric(OtlpHttpCollector collector, string name) =>
        GetMetricNames(collector).Contains(name);

    private static bool HasServiceName(OtlpHttpCollector collector)
    {
        static bool Matches(IEnumerable<KeyValue> attributes) =>
            attributes.Any((p) => p.Key == "service.name" && p.Value?.StringValue == ServiceName);

        return
            collector.GetLogsRequests().SelectMany((p) => p.ResourceLogs).Any((r) => Matches(r.Resource.Attributes)) ||
            collector.GetMetricsRequests().SelectMany((p) => p.ResourceMetrics).Any((r) => Matches(r.Resource.Attributes)) ||
            collector.GetTraceRequests().SelectMany((p) => p.ResourceSpans).Any((r) => Matches(r.Resource.Attributes));
    }

    private static List<(string Scope, Proto.Trace.V1.Span Span)> GetSpans(OtlpHttpCollector collector) =>
        [.. collector.GetTraceRequests()
            .SelectMany((p) => p.ResourceSpans)
            .SelectMany((p) => p.ScopeSpans)
            .SelectMany((p) => p.Spans.Select((r) => (p.Scope?.Name ?? string.Empty, r)))];

    private static HashSet<string> GetMetricNames(OtlpHttpCollector collector) =>
        collector.GetMetricsRequests()
            .SelectMany((p) => p.ResourceMetrics)
            .SelectMany((p) => p.ScopeMetrics)
            .SelectMany((p) => p.Metrics)
            .Select((p) => p.Name)
            .ToHashSet(StringComparer.Ordinal);

    private static List<LogRecord> GetLogRecords(OtlpHttpCollector collector) =>
        [.. collector.GetLogsRequests()
            .SelectMany((p) => p.ResourceLogs)
            .SelectMany((p) => p.ScopeLogs)
            .SelectMany((p) => p.LogRecords)];

    private static string Detail(OtlpHttpCollector collector) =>
        $"Spans seen: {string.Join(", ", GetSpans(collector).Select(t => $"{t.Scope}/{t.Span.Name}"))}." +
        $"{Environment.NewLine}Metrics seen: {string.Join(", ", GetMetricNames(collector))}." +
        $"{Environment.NewLine}Logs seen: {GetLogRecords(collector).Count}." +
        $"{Environment.NewLine}{collector.GetRawHitSummary()}";
}
