// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MeterProviderSdkTest
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

        var metricInstrumentIgnoredEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 33 && e.Payload[1] as string == meterName);

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
    public void MeterProviderSdkAddMeterWithPredicate()
    {
        using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.A", "1.0.0");
        using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.A", "2.0.0");
        using var meter3 = new Meter($"{Utils.GetCurrentMethodName()}.B");
        using var meter4 = new Meter($"B.{Utils.GetCurrentMethodName()}");

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter => meter.Version == "2.0.0" || meter.Name.EndsWith(".B"))
            .AddInMemoryExporter(new List<Metric>())
            .Build())
        {
            Assert.False(IsMeterEnabled(meter1));
            Assert.True(IsMeterEnabled(meter2));
            Assert.True(IsMeterEnabled(meter3));
            Assert.False(IsMeterEnabled(meter4));
        }
    }

    [Fact]
    public void MeterProviderSdkAddMeterWithPredicateException()
    {
        using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.A", "1.0.0");
        using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.B");

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter => meter.Version.StartsWith("1.0.0")) // throws!
            .AddInMemoryExporter(new List<Metric>())
            .Build())
        {
            Assert.True(IsMeterEnabled(meter1));
            Assert.False(IsMeterEnabled(meter2));
        }
    }

    [Fact]
    public void MeterProviderSdkAddMeterWithMultiplePredicates()
    {
        using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.A", "1.0.0");
        using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.A", "2.0.0");
        using var meter3 = new Meter($"{Utils.GetCurrentMethodName()}.B");
        using var meter4 = new Meter($"B.{Utils.GetCurrentMethodName()}");

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter => meter.Version == "2.0.0")
            .AddMeter(meter => meter.Name.StartsWith("B."))
            .AddInMemoryExporter(new List<Metric>())
            .Build())
        {
            Assert.False(IsMeterEnabled(meter1));
            Assert.True(IsMeterEnabled(meter2));
            Assert.False(IsMeterEnabled(meter3));
            Assert.True(IsMeterEnabled(meter4));
        }
    }

    [Fact]
    public void MeterProviderSdkAddMeterWithWildCardAndPredicate()
    {
        using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.A", "1.0.0");
        using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.A", "2.0.0");
        using var meter3 = new Meter($"{Utils.GetCurrentMethodName()}.B");
        using var meter4 = new Meter($"B.{Utils.GetCurrentMethodName()}");

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter => meter.Version == "2.0.0")
            .AddMeter("B.*")
            .AddInMemoryExporter(new List<Metric>())
            .Build())
        {
            Assert.False(IsMeterEnabled(meter1));
            Assert.True(IsMeterEnabled(meter2));
            Assert.False(IsMeterEnabled(meter3));
            Assert.True(IsMeterEnabled(meter4));
        }
    }

    [Fact]
    public void MeterProviderSdkAddMeterWithConflictingWildCardAndPredicate()
    {
        using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.A", "1.0.0");
        using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.B");
        using var meter3 = new Meter($"B.{Utils.GetCurrentMethodName()}");

        using (var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter => !meter.Name.StartsWith("B.*"))
            .AddMeter("B.*")
            .AddInMemoryExporter(new List<Metric>())
            .Build())
        {
            Assert.True(IsMeterEnabled(meter1));
            Assert.True(IsMeterEnabled(meter2));
            Assert.True(IsMeterEnabled(meter3));
        }
    }

    private static bool IsMeterEnabled(Meter meter)
    {
        var counter = meter.CreateCounter<int>("test");
        return counter.Enabled;
    }
}
