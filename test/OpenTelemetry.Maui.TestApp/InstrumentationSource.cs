// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.Maui.TestApp;

/// <summary>
/// Holds the <see cref="ActivitySource"/> and <see cref="Meter"/> (plus their
/// instruments) exercised by the on-device tests. The names and values defined
/// here are the contract the host <c>OpenTelemetry.Maui.Tests</c> orchestrator
/// asserts against after the telemetry is exported over OTLP/HTTP.
/// </summary>
public sealed class InstrumentationSource : IDisposable
{
    // 10.0.2.2 is the Android emulator's alias for the host loopback, which is
    // where the orchestrator runs the collector the app exports to.
    public const string OtlpEndpoint = "http://10.0.2.2:4318";

    public const string ServiceName = "otel-maui-testapp";

    public const string ActivitySourceName = "OpenTelemetry.Maui.TestApp.Traces";
    public const string ActivityName = "MauiScenario";
    public const string ActivityTagKey = "otel.maui.scenario";
    public const string ActivityTagValue = "end-to-end";

    public const string MeterName = "OpenTelemetry.Maui.TestApp.Metrics";
    public const string CounterName = "maui.scenario.count";
    public const string HistogramName = "maui.scenario.duration";

    public const string LoggerName = "OpenTelemetry.Maui.TestApp.Logs";
    public const string LogBody = "MAUI end-to-end scenario executed";

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
