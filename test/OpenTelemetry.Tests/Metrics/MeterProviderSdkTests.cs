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
}
