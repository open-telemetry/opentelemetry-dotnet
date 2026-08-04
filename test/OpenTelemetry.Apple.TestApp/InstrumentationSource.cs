// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Apple.TestApp;

/// <summary>
/// Holds the <see cref="ActivitySource"/> and <see cref="Meter"/> (plus their
/// instruments) exercised by the on-device tests. The names and values defined
/// here are the contract the host <c>OpenTelemetry.Apple.Tests</c> orchestrator
/// asserts against after the telemetry is exported over OTLP/HTTP.
/// </summary>
public sealed class InstrumentationSource : IDisposable
{
    public const string OtlpEndpoint = "http://localhost:4318";

    public const string ServiceName = "otel-apple-testapp";

    public const string ActivitySourceName = "OpenTelemetry.Apple.TestApp.Traces";
    public const string ActivityName = "AppleScenario";
    public const string ActivityTagKey = "otel.apple.scenario";
    public const string ActivityTagValue = "end-to-end";

    public const string MeterName = "OpenTelemetry.Apple.TestApp.Metrics";
    public const string CounterName = "apple.scenario.count";
    public const string HistogramName = "apple.scenario.duration";

    public const string LoggerName = "OpenTelemetry.Apple.TestApp.Logs";
    public const string LogBody = "Apple end-to-end scenario executed";

    private readonly Meter meter;

    public InstrumentationSource()
    {
        var version = typeof(InstrumentationSource).Assembly.GetName().Version?.ToString();
        this.ActivitySource = new(new ActivitySourceOptions(ActivitySourceName) { Version = version });
        this.meter = new(new MeterOptions(MeterName) { Version = version });
        this.Counter = this.meter.CreateCounter<long>(CounterName);
        this.Histogram = this.meter.CreateHistogram<double>(HistogramName);
    }

    public ActivitySource ActivitySource { get; }

    public Counter<long> Counter { get; }

    public Histogram<double> Histogram { get; }

    public void Dispose()
    {
        this.meter.Dispose();
        this.ActivitySource.Dispose();
    }
}
