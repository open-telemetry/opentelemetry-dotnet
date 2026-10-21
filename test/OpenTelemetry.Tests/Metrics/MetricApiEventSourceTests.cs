// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Metrics.Tests;

[Collection(EventSourceCollectionDefinition.Name)]
public class MetricApiEventSourceTests : MetricTestsBase
{
    private const int MaxTimeToAllowForFlush = 10000;

    [Fact]
    public void UnsupportedMetricInstrument()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems));

        using (var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log))
        {
            var counter = meter.CreateCounter<decimal>("counter");
            counter.Add(1);

            // This validates that we log InstrumentIgnored event
            // and not something else.
            var instrumentIgnoredEvents = eventListener.Messages.Where((e) => e.EventId == 33);
#if BUILDING_HOSTING_TESTS
            // Note: When using IMetricsListener this event is fired twice. Once
            // for the SDK listener ignoring it because it isn't listening to
            // the meter and then once for IMetricsListener ignoring it because
            // decimal is not supported.
            Assert.Equal(2, instrumentIgnoredEvents.Count());
#else
            Assert.Single(instrumentIgnoredEvents);
#endif
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Empty(exportedItems);
    }
}
