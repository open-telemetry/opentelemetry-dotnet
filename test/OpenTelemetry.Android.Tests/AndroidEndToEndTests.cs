// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Proto.Common.V1;
using OpenTelemetry.Proto.Logs.V1;

namespace OpenTelemetry.Android.Tests;

public sealed class AndroidEndToEndTests(AndroidAppFixture fixture) : IClassFixture<AndroidAppFixture>
{
    private const string ServiceName = "otel-android-testapp";
    private const string ActivityName = "AndroidScenario";
    private const string ActivityTagKey = "otel.android.scenario";
    private const string ActivityTagValue = "end-to-end";
    private const string CounterName = "android.scenario.count";
    private const string HistogramName = "android.scenario.duration";
    private const string LogBody = "Android end-to-end scenario executed";

    private static readonly TimeSpan CollectTimeout = TimeSpan.FromSeconds(30);

    private readonly AndroidAppFixture fixture = fixture;

    [Fact]
    public void DeviceTestRunSucceeded() =>
        Assert.True(
            this.fixture.DeviceRunExitCode == 0,
            $"On-device test run failed with exit code {this.fixture.DeviceRunExitCode}.{Environment.NewLine}{this.fixture.DeviceRunOutput}");

    [Fact]
    public async Task LogsAreExported()
    {
        var collector = this.fixture.Collector;

        await WaitForAsync(() => HasLog(collector, LogBody), () => Detail(collector));

        var scenarioLog = GetLogRecords(collector).First((p) => Body(p).Contains(LogBody, StringComparison.Ordinal));

        Assert.Equal((int)SeverityNumber.Info, (int)scenarioLog.SeverityNumber);
        Assert.True(HasServiceName(collector), $"Expected resource attribute service.name='{ServiceName}'.");
    }

    [Fact]
    public async Task MetricsAreExported()
    {
        var collector = this.fixture.Collector;

        await WaitForAsync(
            () => HasMetric(collector, CounterName) && HasMetric(collector, HistogramName),
            () => Detail(collector));

        Assert.True(HasMetric(collector, CounterName), $"Expected metric '{CounterName}'.");
        Assert.True(HasMetric(collector, HistogramName), $"Expected metric '{HistogramName}'.");
        Assert.True(HasServiceName(collector), $"Expected resource attribute service.name='{ServiceName}'.");
    }

    [Fact]
    public async Task TracesAreExported()
    {
        var collector = this.fixture.Collector;

        await WaitForAsync(() => HasScenarioTrace(collector), () => Detail(collector));

        Assert.True(HasScenarioTrace(collector), "Expected a scenario span with the contract name and tag.");
        Assert.True(HasServiceName(collector), $"Expected resource attribute service.name='{ServiceName}'.");
    }

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
