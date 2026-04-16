// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using Xunit;

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
    public void TransientMeterExhaustsMetricStorageTest(bool withView, bool forceFlushAfterEachTest)
    {
        using var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log);

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
            Assert.Empty(exportedItems);
        }
        else
        {
            meterProvider.ForceFlush();

            Assert.Single(exportedItems);
        }

        var metricInstrumentIgnoredEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 33 && (e.Payload?.Count ?? 0) >= 2 && e.Payload![1] as string == meterName);

        Assert.Single(metricInstrumentIgnoredEvents);

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
    public void NonFiniteCounterDoubleMeasurementsAreDropped()
    {
        var meterName = Utils.GetCurrentMethodName();
        var exportedItems = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(exportedItems)
            .Build();

        using var meter = new Meter(meterName);
        var counter = meter.CreateCounter<double>("counter");

        counter.Add(double.NaN);
        counter.Add(double.PositiveInfinity);
        counter.Add(double.NegativeInfinity);
        counter.Add(2.5);

        Assert.True(meterProvider.ForceFlush());

        var exportedMetric = Assert.Single(exportedItems);
        var metric = new MetricSnapshot(exportedMetric);
        var metricPoint = Assert.Single(metric.MetricPoints);
        Assert.Equal(2.5, metricPoint.GetSumDouble());
    }

    [Fact]
    public void NonFiniteUpDownCounterDoubleMeasurementsAreDropped()
    {
        var meterName = Utils.GetCurrentMethodName();
        var exportedItems = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(exportedItems)
            .Build();

        using var meter = new Meter(meterName);
        var upDownCounter = meter.CreateUpDownCounter<double>("updowncounter");

        upDownCounter.Add(double.PositiveInfinity);
        upDownCounter.Add(double.NegativeInfinity);
        upDownCounter.Add(2.5);

        Assert.True(meterProvider.ForceFlush());

        var exportedMetric = Assert.Single(exportedItems);
        var metric = new MetricSnapshot(exportedMetric);
        var metricPoint = Assert.Single(metric.MetricPoints);
        Assert.Equal(2.5, metricPoint.GetSumDouble());
    }

    [Fact]
    public void NaNHistogramMeasurementsAreNotDropped()
    {
        var meterName = Utils.GetCurrentMethodName();
        var exportedItems = new List<Metric>();

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meterName)
            .AddInMemoryExporter(exportedItems)
            .Build();

        using var meter = new Meter(meterName);
        var histogram = meter.CreateHistogram<double>("histogram");

        histogram.Record(18);
        histogram.Record(double.NaN);

        Assert.True(meterProvider.ForceFlush());

        var exportedMetric = Assert.Single(exportedItems);
        var metric = new MetricSnapshot(exportedMetric);
        var metricPoint = Assert.Single(metric.MetricPoints);
        Assert.True(double.IsNaN(metricPoint.GetHistogramSum()));
    }
}
