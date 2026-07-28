// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Android.TestApp;

/// <summary>
/// Holds the <see cref="ActivitySource"/> and <see cref="Meter"/> (plus their
/// instruments) exercised by the on-device tests. The names and values defined
/// here are the contract the host <c>OpenTelemetry.Android.Tests</c> orchestrator
/// asserts against after the telemetry is exported over OTLP/HTTP.
/// </summary>
public sealed class InstrumentationSource : IDisposable
{
    public const string ServiceName = "otel-android-testapp";

    public const string ActivitySourceName = "OpenTelemetry.Android.TestApp.Traces";
    public const string ActivityName = "AndroidScenario";
    public const string ActivityTagKey = "otel.android.scenario";
    public const string ActivityTagValue = "end-to-end";

    public const string MeterName = "OpenTelemetry.Android.TestApp.Metrics";
    public const string CounterName = "android.scenario.count";
    public const string HistogramName = "android.scenario.duration";

    public const string LoggerName = "OpenTelemetry.Android.TestApp.Logs";
    public const string LogBody = "Android end-to-end scenario executed";

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
