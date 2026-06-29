// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;

namespace OpenTelemetry.Metrics.Tests;

public class MeterProviderSdkTests
{
    [Fact]
    public void BuilderTypeDoesNotChangeTest()
    {
        var originalBuilder = Sdk.CreateMeterProviderBuilder();
        var currentBuilder = originalBuilder;

        var deferredBuilder = currentBuilder as IDeferredMeterProviderBuilder;
        Assert.NotNull(deferredBuilder);

        currentBuilder = deferredBuilder.Configure((sp, innerBuilder) => { });
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.ConfigureServices(s => { });
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.AddInstrumentation(() => new object());
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        currentBuilder = currentBuilder.AddMeter("MySource");
        Assert.True(ReferenceEquals(originalBuilder, currentBuilder));

        using var provider = currentBuilder.Build();

        Assert.NotNull(provider);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, true)]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public void TransientMeterMetricStorageIsReclaimedOnCollectTest(bool withView, bool forceFlushAfterEachTest)
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var meterName = Utils.GetCurrentMethodName();
        var exportedItems = new List<Metric>();

        var builder = Sdk.CreateMeterProviderBuilder()
            .SetMaxMetricStreams(1)
            .AddMeter(meterName)
            .AddInMemoryExporter(exportedItems);

        if (withView)
        {
            builder.AddView(i => null);
        }

        using var meterProvider = builder
            .Build() as MeterProviderSdk;

        Assert.NotNull(meterProvider);

        RunTest();

        if (forceFlushAfterEachTest)
        {
            Assert.Single(exportedItems);
        }

        RunTest();

        if (forceFlushAfterEachTest)
        {
            // A collection happened after the first meter was disposed, so its
            // storage slot was reclaimed and reused by the second meter's
            // instrument, which is therefore exported rather than dropped.
            Assert.Single(exportedItems);
        }
        else
        {
            meterProvider.ForceFlush();

            Assert.Single(exportedItems);
        }

        var metricInstrumentIgnoredEvents = eventListener.Messages.Where((e) => e.EventId == 33 && (e.Payload?.Count ?? 0) >= 2 && (e.Payload![1] as string) == meterName);

        if (forceFlushAfterEachTest)
        {
            // Storage was reclaimed between the two meters, so no instrument was dropped.
            Assert.Empty(metricInstrumentIgnoredEvents);
        }
        else
        {
            // No collection happened between the two meters, so the first
            // metric's storage had not yet been reclaimed and the second
            // instrument was dropped because the stream limit was reached.
            Assert.Single(metricInstrumentIgnoredEvents);
        }

        void RunTest()
        {
            exportedItems.Clear();

            var meter = new Meter(meterName);

            var counter = meter.CreateCounter<int>("Counter");
            counter.Add(1);

            meter.Dispose();

            if (forceFlushAfterEachTest)
            {
                meterProvider.ForceFlush();
            }
        }
    }

    [Fact]
    public void TransientMeterMetricStorageIsReusedAcrossManyCollectsTest()
    {
        using var eventListener = new TestEventListener(OpenTelemetrySdkEventSource.Log);

        var meterName = Utils.GetCurrentMethodName();
        var exportedItems = new List<Metric>();

        const int MaxMetricStreams = 2;

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .SetMaxMetricStreams(MaxMetricStreams)
            .AddMeter(meterName)
            .AddInMemoryExporter(exportedItems)
            .Build() as MeterProviderSdk;

        Assert.NotNull(meterProvider);

        // Create and dispose many more instruments than the metric stream limit,
        // collecting after each one so the deactivated metric's slot is reclaimed.
        for (var i = 0; i < MaxMetricStreams * 10; i++)
        {
            exportedItems.Clear();

            using (var meter = new Meter(meterName))
            {
                var counter = meter.CreateCounter<int>("Counter");
                counter.Add(1);
            }

            meterProvider.ForceFlush();

            Assert.Single(exportedItems);
            Assert.Equal("Counter", exportedItems[0].Name);
        }

        // No instrument should ever have been dropped due to exhausted storage.
        var metricInstrumentIgnoredEvents = eventListener.Messages.Where((e) => e.EventId == 33 && (e.Payload?.Count ?? 0) >= 2 && (e.Payload![1] as string) == meterName);

        Assert.Empty(metricInstrumentIgnoredEvents);
    }
}
