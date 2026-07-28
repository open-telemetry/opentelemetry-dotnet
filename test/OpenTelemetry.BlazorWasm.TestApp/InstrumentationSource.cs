// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace OpenTelemetry.BlazorWasm.TestApp;

/// <summary>
/// Holds the <see cref="ActivitySource"/> and <see cref="Meter"/> (plus their
/// instruments) exercised by the application. The names and values defined here
/// are the contract asserted by the end-to-end test.
/// </summary>
public sealed class InstrumentationSource : IDisposable
{
    public const string ServiceName = "otel-blazor-wasm-testapp";

    public const string ActivitySourceName = "OpenTelemetry.BlazorWasm.TestApp.Traces";
    public const string ActivityName = "BlazorWasmScenario";
    public const string ActivityTagKey = "otel.blazor.scenario";
    public const string ActivityTagValue = "end-to-end";

    public const string MeterName = "OpenTelemetry.BlazorWasm.TestApp.Metrics";
    public const string CounterName = "blazor.wasm.scenario.count";
    public const string HistogramName = "blazor.wasm.scenario.duration";

    private readonly ActivitySource activitySource;
    private readonly Meter meter;

    public InstrumentationSource()
    {
        var version = typeof(InstrumentationSource).Assembly.GetName().Version?.ToString();
        this.activitySource = new(new ActivitySourceOptions(ActivitySourceName) { Version = version });
        this.meter = new(new MeterOptions(MeterName) { Version = version });
        this.Counter = this.meter.CreateCounter<long>(CounterName);
        this.Histogram = this.meter.CreateHistogram<double>(HistogramName);
    }

    public ActivitySource ActivitySource => this.activitySource;

    public Counter<long> Counter { get; }

    public Histogram<double> Histogram { get; }

    public void Dispose()
    {
        this.meter.Dispose();
        this.activitySource.Dispose();
    }
}
