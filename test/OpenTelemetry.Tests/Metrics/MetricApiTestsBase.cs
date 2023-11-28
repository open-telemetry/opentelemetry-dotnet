// <copyright file="MetricApiTestsBase.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests;

#pragma warning disable SA1402

public abstract class MetricApiTestsBase : MetricTestsBase
{
    private const int MaxTimeToAllowForFlush = 10000;
    private static readonly int NumberOfThreads = Environment.ProcessorCount;
    private static readonly long DeltaLongValueUpdatedByEachCall = 10;
    private static readonly double DeltaDoubleValueUpdatedByEachCall = 11.987;
    private static readonly int NumberOfMetricUpdateByEachThread = 100000;
    private readonly ITestOutputHelper output;
    private readonly IConfiguration configuration;

    protected MetricApiTestsBase(ITestOutputHelper output, bool emitOverflowAttribute, bool shouldReclaimUnusedMetricPoints)
    {
        this.output = output;

        var configurationData = new Dictionary<string, string>();

        if (emitOverflowAttribute)
        {
            configurationData[EmitOverFlowAttributeConfigKey] = "true";
        }

        if (shouldReclaimUnusedMetricPoints)
        {
            configurationData[ReclaimUnusedMetricPointsConfigKey] = "true";
        }

        this.configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configurationData)
            .Build();
    }

    [Fact]
    public void MeasurementWithNullValuedTag()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        var counter = meter.CreateCounter<long>("myCounter");
        counter.Add(100, new KeyValuePair<string, object>("tagWithNullValue", null));

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal("myCounter", metric.Name);
        List<MetricPoint> metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);
        var metricPoint = metricPoints[0];
        Assert.Equal(100, metricPoint.GetSumLong());
        Assert.Equal(1, metricPoint.Tags.Count);
        var tagEnumerator = metricPoint.Tags.GetEnumerator();
        tagEnumerator.MoveNext();
        Assert.Equal("tagWithNullValue", tagEnumerator.Current.Key);
        Assert.Null(tagEnumerator.Current.Value);
    }

    [Fact]
    public void ObserverCallbackTest()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        var measurement = new Measurement<int>(100, new("name", "apple"), new("color", "red"));
        meter.CreateObservableGauge("myGauge", () => measurement);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal("myGauge", metric.Name);
        List<MetricPoint> metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);
        var metricPoint = metricPoints[0];
        Assert.Equal(100, metricPoint.GetGaugeLastValueLong());
        Assert.True(metricPoint.Tags.Count > 0);
    }

    [Fact]
    public void ObserverCallbackExceptionTest()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        var measurement = new Measurement<int>(100, new("name", "apple"), new("color", "red"));
        meter.CreateObservableGauge("myGauge", () => measurement);
        meter.CreateObservableGauge<long>("myBadGauge", observeValues: () => throw new Exception("gauge read error"));

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal("myGauge", metric.Name);
        List<MetricPoint> metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);
        var metricPoint = metricPoints[0];
        Assert.Equal(100, metricPoint.GetGaugeLastValueLong());
        Assert.True(metricPoint.Tags.Count > 0);
    }

    [Theory]
    [InlineData("unit")]
    [InlineData("")]
    [InlineData(null)]
    public void MetricUnitIsExportedCorrectly(string unit)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems);

        using var meterProvider = meterProviderBuilder.Build();

        var counter = meter.CreateCounter<long>("name1", unit);
        counter.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal(unit ?? string.Empty, metric.Unit);
    }

    [Theory]
    [InlineData("description")]
    [InlineData("")]
    [InlineData(null)]
    public void MetricDescriptionIsExportedCorrectly(string description)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems);

        using var meterProvider = meterProviderBuilder.Build();

        var counter = meter.CreateCounter<long>("name1", null, description);
        counter.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal(description ?? string.Empty, metric.Description);
    }

    [Fact]
    public void MetricInstrumentationScopeIsExportedCorrectly()
    {
        var exportedItems = new List<Metric>();
        var meterName = Utils.GetCurrentMethodName();
        var meterVersion = "1.0";
        var meterTags = new List<KeyValuePair<string, object?>>
        {
            new(
                "MeterTagKey",
                "MeterTagValue"),
        };
        using var meter = new Meter($"{meterName}", meterVersion, meterTags);
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems);

        using var meterProvider = meterProviderBuilder.Build();

        var counter = meter.CreateCounter<long>("name1");
        counter.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal(meterName, metric.MeterName);
        Assert.Equal(meterVersion, metric.MeterVersion);

        bool containsMeterTags = metric.MeterTags.Any(kvp =>
            kvp.Key == meterTags[0].Key && Equals(kvp.Value, meterTags[0].Value));
        Assert.True(containsMeterTags);
    }

    [Fact]
    public void DuplicateInstrumentRegistration_NoViews_IdenticalInstruments()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems);

        using var meterProvider = meterProviderBuilder.Build();

        var instrument = meter.CreateCounter<long>("instrumentName", "instrumentUnit", "instrumentDescription");
        var duplicateInstrument = meter.CreateCounter<long>("instrumentName", "instrumentUnit", "instrumentDescription");

        instrument.Add(10);
        duplicateInstrument.Add(20);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);

        var metric = exportedItems[0];
        Assert.Equal("instrumentName", metric.Name);
        List<MetricPoint> metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);
        var metricPoint1 = metricPoints[0];
        Assert.Equal(30, metricPoint1.GetSumLong());
    }

    [Fact]
    public void DuplicateInstrumentRegistration_NoViews_DuplicateInstruments_DifferentDescription()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems);

        using var meterProvider = meterProviderBuilder.Build();

        var instrument = meter.CreateCounter<long>("instrumentName", "instrumentUnit", "instrumentDescription1");
        var duplicateInstrument = meter.CreateCounter<long>("instrumentName", "instrumentUnit", "instrumentDescription2");

        instrument.Add(10);
        duplicateInstrument.Add(20);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);

        var metric1 = exportedItems[0];
        var metric2 = exportedItems[1];
        Assert.Equal("instrumentDescription1", metric1.Description);
        Assert.Equal("instrumentDescription2", metric2.Description);

        List<MetricPoint> metric1MetricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric1.GetMetricPoints())
        {
            metric1MetricPoints.Add(mp);
        }

        Assert.Single(metric1MetricPoints);
        var metricPoint1 = metric1MetricPoints[0];
        Assert.Equal(10, metricPoint1.GetSumLong());

        List<MetricPoint> metric2MetricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric2.GetMetricPoints())
        {
            metric2MetricPoints.Add(mp);
        }

        Assert.Single(metric2MetricPoints);
        var metricPoint2 = metric2MetricPoints[0];
        Assert.Equal(20, metricPoint2.GetSumLong());
    }

    [Fact]
    public void DuplicateInstrumentRegistration_NoViews_DuplicateInstruments_DifferentUnit()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems);

        using var meterProvider = meterProviderBuilder.Build();

        var instrument = meter.CreateCounter<long>("instrumentName", "instrumentUnit1", "instrumentDescription");
        var duplicateInstrument = meter.CreateCounter<long>("instrumentName", "instrumentUnit2", "instrumentDescription");

        instrument.Add(10);
        duplicateInstrument.Add(20);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);

        var metric1 = exportedItems[0];
        var metric2 = exportedItems[1];
        Assert.Equal("instrumentUnit1", metric1.Unit);
        Assert.Equal("instrumentUnit2", metric2.Unit);

        List<MetricPoint> metric1MetricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric1.GetMetricPoints())
        {
            metric1MetricPoints.Add(mp);
        }

        Assert.Single(metric1MetricPoints);
        var metricPoint1 = metric1MetricPoints[0];
        Assert.Equal(10, metricPoint1.GetSumLong());

        List<MetricPoint> metric2MetricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric2.GetMetricPoints())
        {
            metric2MetricPoints.Add(mp);
        }

        Assert.Single(metric2MetricPoints);
        var metricPoint2 = metric2MetricPoints[0];
        Assert.Equal(20, metricPoint2.GetSumLong());
    }

    [Fact]
    public void DuplicateInstrumentRegistration_NoViews_DuplicateInstruments_DifferentDataType()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems);

        using var meterProvider = meterProviderBuilder.Build();

        var instrument = meter.CreateCounter<long>("instrumentName", "instrumentUnit", "instrumentDescription");
        var duplicateInstrument = meter.CreateCounter<double>("instrumentName", "instrumentUnit", "instrumentDescription");

        instrument.Add(10);
        duplicateInstrument.Add(20);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);

        var metric1 = exportedItems[0];
        var metric2 = exportedItems[1];

        List<MetricPoint> metric1MetricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric1.GetMetricPoints())
        {
            metric1MetricPoints.Add(mp);
        }

        Assert.Single(metric1MetricPoints);
        var metricPoint1 = metric1MetricPoints[0];
        Assert.Equal(10, metricPoint1.GetSumLong());

        List<MetricPoint> metric2MetricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric2.GetMetricPoints())
        {
            metric2MetricPoints.Add(mp);
        }

        Assert.Single(metric2MetricPoints);
        var metricPoint2 = metric2MetricPoints[0];
        Assert.Equal(20D, metricPoint2.GetSumDouble());
    }

    [Fact]
    public void DuplicateInstrumentRegistration_NoViews_DuplicateInstruments_DifferentInstrumentType()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems);

        using var meterProvider = meterProviderBuilder.Build();

        var instrument = meter.CreateCounter<long>("instrumentName", "instrumentUnit", "instrumentDescription");
        var duplicateInstrument = meter.CreateHistogram<long>("instrumentName", "instrumentUnit", "instrumentDescription");

        instrument.Add(10);
        duplicateInstrument.Record(20);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);

        var metric1 = exportedItems[0];
        var metric2 = exportedItems[1];

        List<MetricPoint> metric1MetricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric1.GetMetricPoints())
        {
            metric1MetricPoints.Add(mp);
        }

        Assert.Single(metric1MetricPoints);
        var metricPoint1 = metric1MetricPoints[0];
        Assert.Equal(10, metricPoint1.GetSumLong());

        List<MetricPoint> metric2MetricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric2.GetMetricPoints())
        {
            metric2MetricPoints.Add(mp);
        }

        Assert.Single(metric2MetricPoints);
        var metricPoint2 = metric2MetricPoints[0];
        Assert.Equal(1, metricPoint2.GetHistogramCount());
        Assert.Equal(20D, metricPoint2.GetHistogramSum());
    }

    [Fact]
    public void DuplicateInstrumentNamesFromDifferentMetersWithSameNameDifferentVersion()
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}", "1.0");
        using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}", "2.0");
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter1.Name)
            .AddMeter(meter2.Name)
            .AddInMemoryExporter(exportedItems);

        using var meterProvider = meterProviderBuilder.Build();

        // Expecting one metric stream.
        var counterLong = meter1.CreateCounter<long>("name1");
        counterLong.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);

        // Expeecting another metric stream since the meter differs by version
        var anotherCounterSameNameDiffMeter = meter2.CreateCounter<long>("name1");
        anotherCounterSameNameDiffMeter.Add(10);
        counterLong.Add(10);
        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, true)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, false)]
    [InlineData(MetricReaderTemporalityPreference.Delta, true)]
    [InlineData(MetricReaderTemporalityPreference.Delta, false)]
    public void DuplicateInstrumentNamesFromDifferentMetersAreAllowed(MetricReaderTemporalityPreference temporality, bool hasView)
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.1.{temporality}");
        using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.2.{temporality}");
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter1.Name)
            .AddMeter(meter2.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            });

        if (hasView)
        {
            meterProviderBuilder.AddView("name1", new MetricStreamConfiguration() { Description = "description" });
        }

        using var meterProvider = meterProviderBuilder.Build();

        // Expecting one metric stream.
        var counterLong = meter1.CreateCounter<long>("name1");
        counterLong.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);

        // The following will not be ignored
        // as it is the same metric name but different meter.
        var anotherCounterSameNameDiffMeter = meter2.CreateCounter<long>("name1");
        anotherCounterSameNameDiffMeter.Add(10);
        counterLong.Add(10);
        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MeterSourcesWildcardSupportMatchTest(bool hasView)
    {
        using var meter1 = new Meter("AbcCompany.XyzProduct.ComponentA");
        using var meter2 = new Meter("abcCompany.xYzProduct.componentC"); // Wildcard match is case insensitive.
        using var meter3 = new Meter("DefCompany.AbcProduct.ComponentC");
        using var meter4 = new Meter("DefCompany.XyzProduct.ComponentC"); // Wildcard match supports matching multiple patterns.
        using var meter5 = new Meter("GhiCompany.qweProduct.ComponentN");
        using var meter6 = new Meter("SomeCompany.SomeProduct.SomeComponent");

        var exportedItems = new List<Metric>();
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter("AbcCompany.XyzProduct.Component?")
            .AddMeter("DefCompany.*.ComponentC")
            .AddMeter("GhiCompany.qweProduct.ComponentN") // Mixing of non-wildcard meter name and wildcard meter name.
            .AddInMemoryExporter(exportedItems);

        if (hasView)
        {
            meterProviderBuilder.AddView("myGauge1", "newName");
        }

        using var meterProvider = meterProviderBuilder.Build();

        var measurement = new Measurement<int>(100, new("name", "apple"), new("color", "red"));
        meter1.CreateObservableGauge("myGauge1", () => measurement);
        meter2.CreateObservableGauge("myGauge2", () => measurement);
        meter3.CreateObservableGauge("myGauge3", () => measurement);
        meter4.CreateObservableGauge("myGauge4", () => measurement);
        meter5.CreateObservableGauge("myGauge5", () => measurement);
        meter6.CreateObservableGauge("myGauge6", () => measurement);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.True(exportedItems.Count == 5); // "SomeCompany.SomeProduct.SomeComponent" will not be subscribed.

        if (hasView)
        {
            Assert.Equal("newName", exportedItems[0].Name);
        }
        else
        {
            Assert.Equal("myGauge1", exportedItems[0].Name);
        }

        Assert.Equal("myGauge2", exportedItems[1].Name);
        Assert.Equal("myGauge3", exportedItems[2].Name);
        Assert.Equal("myGauge4", exportedItems[3].Name);
        Assert.Equal("myGauge5", exportedItems[4].Name);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MeterSourcesWildcardSupportNegativeTestNoMeterAdded(bool hasView)
    {
        using var meter1 = new Meter($"AbcCompany.XyzProduct.ComponentA.{hasView}");
        using var meter2 = new Meter($"abcCompany.xYzProduct.componentC.{hasView}");

        var exportedItems = new List<Metric>();
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddInMemoryExporter(exportedItems);

        if (hasView)
        {
            meterProviderBuilder.AddView("gauge1", "renamed");
        }

        using var meterProvider = meterProviderBuilder.Build();
        var measurement = new Measurement<int>(100, new("name", "apple"), new("color", "red"));

        meter1.CreateObservableGauge("myGauge1", () => measurement);
        meter2.CreateObservableGauge("myGauge2", () => measurement);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.True(exportedItems.Count == 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CounterAggregationTest(bool exportDelta)
    {
        DateTime testStartTime = DateTime.UtcNow;

        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
        var counterLong = meter.CreateCounter<long>("mycounter");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = exportDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
            })
            .Build();

        counterLong.Add(10);
        counterLong.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        long sumReceived = GetLongSum(exportedItems);
        Assert.Equal(20, sumReceived);

        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);

        DateTimeOffset firstRunStartTime = metricPoint.Value.StartTime;
        DateTimeOffset firstRunEndTime = metricPoint.Value.EndTime;

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        counterLong.Add(10);
        counterLong.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        sumReceived = GetLongSum(exportedItems);
        if (exportDelta)
        {
            Assert.Equal(20, sumReceived);
        }
        else
        {
            Assert.Equal(40, sumReceived);
        }

        metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        if (exportDelta)
        {
            Assert.True(metricPoint.Value.StartTime == firstRunEndTime);
        }
        else
        {
            Assert.Equal(firstRunStartTime, metricPoint.Value.StartTime);
        }

        Assert.True(metricPoint.Value.EndTime > firstRunEndTime);

        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        sumReceived = GetLongSum(exportedItems);
        if (exportDelta)
        {
            Assert.Equal(0, sumReceived);
        }
        else
        {
            Assert.Equal(40, sumReceived);
        }

        exportedItems.Clear();
        counterLong.Add(40);
        counterLong.Add(20);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        sumReceived = GetLongSum(exportedItems);
        if (exportDelta)
        {
            Assert.Equal(60, sumReceived);
        }
        else
        {
            Assert.Equal(100, sumReceived);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObservableCounterAggregationTest(bool exportDelta)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
        int i = 1;
        var counterLong = meter.CreateObservableCounter(
            "observable-counter",
            () =>
            {
                return new List<Measurement<long>>()
                {
                    new Measurement<long>(i++ * 10),
                };
            });

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = exportDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
            })
            .Build();

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        long sumReceived = GetLongSum(exportedItems);
        Assert.Equal(10, sumReceived);

        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        sumReceived = GetLongSum(exportedItems);
        if (exportDelta)
        {
            Assert.Equal(10, sumReceived);
        }
        else
        {
            Assert.Equal(20, sumReceived);
        }

        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        sumReceived = GetLongSum(exportedItems);
        if (exportDelta)
        {
            Assert.Equal(10, sumReceived);
        }
        else
        {
            Assert.Equal(30, sumReceived);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObservableCounterWithTagsAggregationTest(bool exportDelta)
    {
        var exportedItems = new List<Metric>();
        var tags1 = new List<KeyValuePair<string, object>>
        {
            new("statusCode", 200),
            new("verb", "get"),
        };

        var tags2 = new List<KeyValuePair<string, object>>
        {
            new("statusCode", 200),
            new("verb", "post"),
        };

        var tags3 = new List<KeyValuePair<string, object>>
        {
            new("statusCode", 500),
            new("verb", "get"),
        };

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
        var counterLong = meter.CreateObservableCounter(
            "observable-counter",
            () =>
            {
                return new List<Measurement<long>>()
                {
                    new Measurement<long>(10, tags1),
                    new Measurement<long>(10, tags2),
                    new Measurement<long>(10, tags3),
                };
            });

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = exportDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
            })
            .Build();

        // Export 1
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal("observable-counter", metric.Name);
        List<MetricPoint> metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Equal(3, metricPoints.Count);

        var metricPoint1 = metricPoints[0];
        Assert.Equal(10, metricPoint1.GetSumLong());
        ValidateMetricPointTags(tags1, metricPoint1.Tags);

        var metricPoint2 = metricPoints[1];
        Assert.Equal(10, metricPoint2.GetSumLong());
        ValidateMetricPointTags(tags2, metricPoint2.Tags);

        var metricPoint3 = metricPoints[2];
        Assert.Equal(10, metricPoint3.GetSumLong());
        ValidateMetricPointTags(tags3, metricPoint3.Tags);

        // Export 2
        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        metric = exportedItems[0];
        Assert.Equal("observable-counter", metric.Name);
        metricPoints.Clear();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Equal(3, metricPoints.Count);

        metricPoint1 = metricPoints[0];
        Assert.Equal(exportDelta ? 0 : 10, metricPoint1.GetSumLong());
        ValidateMetricPointTags(tags1, metricPoint1.Tags);

        metricPoint2 = metricPoints[1];
        Assert.Equal(exportDelta ? 0 : 10, metricPoint2.GetSumLong());
        ValidateMetricPointTags(tags2, metricPoint2.Tags);

        metricPoint3 = metricPoints[2];
        Assert.Equal(exportDelta ? 0 : 10, metricPoint3.GetSumLong());
        ValidateMetricPointTags(tags3, metricPoint3.Tags);
    }

    [Theory(Skip = "Known issue.")]
    [InlineData(true)]
    [InlineData(false)]
    public void ObservableCounterSpatialAggregationTest(bool exportDelta)
    {
        var exportedItems = new List<Metric>();
        var tags1 = new List<KeyValuePair<string, object>>
        {
            new("statusCode", 200),
            new("verb", "get"),
        };

        var tags2 = new List<KeyValuePair<string, object>>
        {
            new("statusCode", 200),
            new("verb", "post"),
        };

        var tags3 = new List<KeyValuePair<string, object>>
        {
            new("statusCode", 500),
            new("verb", "get"),
        };

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
        var counterLong = meter.CreateObservableCounter(
            "requestCount",
            () =>
            {
                return new List<Measurement<long>>()
                {
                    new Measurement<long>(10, tags1),
                    new Measurement<long>(10, tags2),
                    new Measurement<long>(10, tags3),
                };
            });

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = exportDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
            })
            .AddView("requestCount", new MetricStreamConfiguration() { TagKeys = Array.Empty<string>() })
            .Build();

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal("requestCount", metric.Name);
        List<MetricPoint> metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);

        var emptyTags = new List<KeyValuePair<string, object>>();
        var metricPoint1 = metricPoints[0];
        ValidateMetricPointTags(emptyTags, metricPoint1.Tags);

        // This will fail, as SDK is not "spatially" aggregating the
        // requestCount
        Assert.Equal(30, metricPoint1.GetSumLong());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpDownCounterAggregationTest(bool exportDelta)
    {
        DateTime testStartTime = DateTime.UtcNow;

        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
        var counterLong = meter.CreateUpDownCounter<long>("mycounter");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = exportDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
            })
            .Build();

        counterLong.Add(10);
        counterLong.Add(-5);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        long sumReceived = GetLongSum(exportedItems);
        Assert.Equal(5, sumReceived);

        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);

        DateTimeOffset firstRunStartTime = metricPoint.Value.StartTime;
        DateTimeOffset firstRunEndTime = metricPoint.Value.EndTime;

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        counterLong.Add(10);
        counterLong.Add(-5);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        sumReceived = GetLongSum(exportedItems);

        // Same for both cumulative and delta. MetricReaderTemporalityPreference.Delta implies cumulative for UpDownCounters.
        Assert.Equal(10, sumReceived);

        metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);

        // Same for both cumulative and delta. MetricReaderTemporalityPreference.Delta implies cumulative for UpDownCounters.
        Assert.Equal(firstRunStartTime, metricPoint.Value.StartTime);

        Assert.True(metricPoint.Value.EndTime > firstRunEndTime);

        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        sumReceived = GetLongSum(exportedItems);

        // Same for both cumulative and delta. MetricReaderTemporalityPreference.Delta implies cumulative for UpDownCounters.
        Assert.Equal(10, sumReceived);

        exportedItems.Clear();
        counterLong.Add(40);
        counterLong.Add(-20);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        sumReceived = GetLongSum(exportedItems);

        // Same for both cumulative and delta. MetricReaderTemporalityPreference.Delta implies cumulative for UpDownCounters.
        Assert.Equal(30, sumReceived);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObservableUpDownCounterAggregationTest(bool exportDelta)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
        int i = 1;
        var counterLong = meter.CreateObservableUpDownCounter(
            "observable-counter",
            () =>
            {
                return new List<Measurement<long>>()
                {
                    new Measurement<long>(i++ * 10),
                };
            });

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = exportDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
            })
            .Build();

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        long sumReceived = GetLongSum(exportedItems);
        Assert.Equal(10, sumReceived);

        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        sumReceived = GetLongSum(exportedItems);

        // Same for both cumulative and delta. MetricReaderTemporalityPreference.Delta implies cumulative for UpDownCounters.
        Assert.Equal(20, sumReceived);

        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        sumReceived = GetLongSum(exportedItems);

        // Same for both cumulative and delta. MetricReaderTemporalityPreference.Delta implies cumulative for UpDownCounters.
        Assert.Equal(30, sumReceived);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ObservableUpDownCounterWithTagsAggregationTest(bool exportDelta)
    {
        var exportedItems = new List<Metric>();
        var tags1 = new List<KeyValuePair<string, object>>
        {
            new("statusCode", 200),
            new("verb", "get"),
        };

        var tags2 = new List<KeyValuePair<string, object>>
        {
            new("statusCode", 200),
            new("verb", "post"),
        };

        var tags3 = new List<KeyValuePair<string, object>>
        {
            new("statusCode", 500),
            new("verb", "get"),
        };

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
        var counterLong = meter.CreateObservableUpDownCounter(
            "observable-counter",
            () =>
            {
                return new List<Measurement<long>>()
                {
                    new Measurement<long>(10, tags1),
                    new Measurement<long>(10, tags2),
                    new Measurement<long>(10, tags3),
                };
            });

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = exportDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
            })
            .Build();

        // Export 1
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal("observable-counter", metric.Name);
        List<MetricPoint> metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Equal(3, metricPoints.Count);

        var metricPoint1 = metricPoints[0];
        Assert.Equal(10, metricPoint1.GetSumLong());
        ValidateMetricPointTags(tags1, metricPoint1.Tags);

        var metricPoint2 = metricPoints[1];
        Assert.Equal(10, metricPoint2.GetSumLong());
        ValidateMetricPointTags(tags2, metricPoint2.Tags);

        var metricPoint3 = metricPoints[2];
        Assert.Equal(10, metricPoint3.GetSumLong());
        ValidateMetricPointTags(tags3, metricPoint3.Tags);

        // Export 2
        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        metric = exportedItems[0];
        Assert.Equal("observable-counter", metric.Name);
        metricPoints.Clear();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Equal(3, metricPoints.Count);

        // Same for both cumulative and delta. MetricReaderTemporalityPreference.Delta implies cumulative for UpDownCounters.
        metricPoint1 = metricPoints[0];
        Assert.Equal(10, metricPoint1.GetSumLong());
        ValidateMetricPointTags(tags1, metricPoint1.Tags);

        metricPoint2 = metricPoints[1];
        Assert.Equal(10, metricPoint2.GetSumLong());
        ValidateMetricPointTags(tags2, metricPoint2.Tags);

        metricPoint3 = metricPoints[2];
        Assert.Equal(10, metricPoint3.GetSumLong());
        ValidateMetricPointTags(tags3, metricPoint3.Tags);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DimensionsAreOrderInsensitiveWithSortedKeysFirst(bool exportDelta)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
        var counterLong = meter.CreateCounter<long>("Counter");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = exportDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
            })
            .Build();

        // Emit the first metric with the sorted order of tag keys
        counterLong.Add(5, new("Key1", "Value1"), new("Key2", "Value2"), new("Key3", "Value3"));
        counterLong.Add(10, new("Key1", "Value1"), new("Key3", "Value3"), new("Key2", "Value2"));
        counterLong.Add(10, new("Key2", "Value20"), new("Key1", "Value10"), new("Key3", "Value30"));

        // Emit a metric with different set of keys but the same set of values as one of the previous metric points
        counterLong.Add(25, new("Key4", "Value1"), new("Key5", "Value3"), new("Key6", "Value2"));
        counterLong.Add(25, new("Key4", "Value1"), new("Key6", "Value3"), new("Key5", "Value2"));

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        List<KeyValuePair<string, object>> expectedTagsForFirstMetricPoint = new List<KeyValuePair<string, object>>()
        {
            new("Key1", "Value1"),
            new("Key2", "Value2"),
            new("Key3", "Value3"),
        };

        List<KeyValuePair<string, object>> expectedTagsForSecondMetricPoint = new List<KeyValuePair<string, object>>()
        {
            new("Key1", "Value10"),
            new("Key2", "Value20"),
            new("Key3", "Value30"),
        };

        List<KeyValuePair<string, object>> expectedTagsForThirdMetricPoint = new List<KeyValuePair<string, object>>()
        {
            new("Key4", "Value1"),
            new("Key5", "Value3"),
            new("Key6", "Value2"),
        };

        List<KeyValuePair<string, object>> expectedTagsForFourthMetricPoint = new List<KeyValuePair<string, object>>()
        {
            new("Key4", "Value1"),
            new("Key5", "Value2"),
            new("Key6", "Value3"),
        };

        Assert.Equal(4, GetNumberOfMetricPoints(exportedItems));
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFirstMetricPoint, 1);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForSecondMetricPoint, 2);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForThirdMetricPoint, 3);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFourthMetricPoint, 4);
        long sumReceived = GetLongSum(exportedItems);
        Assert.Equal(75, sumReceived);

        exportedItems.Clear();

        counterLong.Add(5, new("Key2", "Value2"), new("Key1", "Value1"), new("Key3", "Value3"));
        counterLong.Add(5, new("Key2", "Value2"), new("Key1", "Value1"), new("Key3", "Value3"));
        counterLong.Add(10, new("Key2", "Value2"), new("Key3", "Value3"), new("Key1", "Value1"));
        counterLong.Add(10, new("Key2", "Value20"), new("Key3", "Value30"), new("Key1", "Value10"));
        counterLong.Add(20, new("Key4", "Value1"), new("Key6", "Value2"), new("Key5", "Value3"));
        counterLong.Add(20, new("Key4", "Value1"), new("Key5", "Value2"), new("Key6", "Value3"));

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.Equal(4, GetNumberOfMetricPoints(exportedItems));
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFirstMetricPoint, 1);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForSecondMetricPoint, 2);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForThirdMetricPoint, 3);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFourthMetricPoint, 4);
        sumReceived = GetLongSum(exportedItems);
        if (exportDelta)
        {
            Assert.Equal(70, sumReceived);
        }
        else
        {
            Assert.Equal(145, sumReceived);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DimensionsAreOrderInsensitiveWithUnsortedKeysFirst(bool exportDelta)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{exportDelta}");
        var counterLong = meter.CreateCounter<long>("Counter");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = exportDelta ? MetricReaderTemporalityPreference.Delta : MetricReaderTemporalityPreference.Cumulative;
            })
            .Build();

        // Emit the first metric with the unsorted order of tag keys
        counterLong.Add(5, new("Key1", "Value1"), new("Key3", "Value3"), new("Key2", "Value2"));
        counterLong.Add(10, new("Key1", "Value1"), new("Key2", "Value2"), new("Key3", "Value3"));
        counterLong.Add(10, new("Key2", "Value20"), new("Key1", "Value10"), new("Key3", "Value30"));

        // Emit a metric with different set of keys but the same set of values as one of the previous metric points
        counterLong.Add(25, new("Key4", "Value1"), new("Key5", "Value3"), new("Key6", "Value2"));
        counterLong.Add(25, new("Key4", "Value1"), new("Key6", "Value3"), new("Key5", "Value2"));

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        List<KeyValuePair<string, object>> expectedTagsForFirstMetricPoint = new List<KeyValuePair<string, object>>()
        {
            new("Key1", "Value1"),
            new("Key2", "Value2"),
            new("Key3", "Value3"),
        };

        List<KeyValuePair<string, object>> expectedTagsForSecondMetricPoint = new List<KeyValuePair<string, object>>()
        {
            new("Key1", "Value10"),
            new("Key2", "Value20"),
            new("Key3", "Value30"),
        };

        List<KeyValuePair<string, object>> expectedTagsForThirdMetricPoint = new List<KeyValuePair<string, object>>()
        {
            new("Key4", "Value1"),
            new("Key5", "Value3"),
            new("Key6", "Value2"),
        };

        List<KeyValuePair<string, object>> expectedTagsForFourthMetricPoint = new List<KeyValuePair<string, object>>()
        {
            new("Key4", "Value1"),
            new("Key5", "Value2"),
            new("Key6", "Value3"),
        };

        Assert.Equal(4, GetNumberOfMetricPoints(exportedItems));
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFirstMetricPoint, 1);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForSecondMetricPoint, 2);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForThirdMetricPoint, 3);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFourthMetricPoint, 4);
        long sumReceived = GetLongSum(exportedItems);
        Assert.Equal(75, sumReceived);

        exportedItems.Clear();

        counterLong.Add(5, new("Key2", "Value2"), new("Key1", "Value1"), new("Key3", "Value3"));
        counterLong.Add(5, new("Key2", "Value2"), new("Key1", "Value1"), new("Key3", "Value3"));
        counterLong.Add(10, new("Key2", "Value2"), new("Key3", "Value3"), new("Key1", "Value1"));
        counterLong.Add(10, new("Key2", "Value20"), new("Key3", "Value30"), new("Key1", "Value10"));
        counterLong.Add(20, new("Key4", "Value1"), new("Key6", "Value2"), new("Key5", "Value3"));
        counterLong.Add(20, new("Key4", "Value1"), new("Key5", "Value2"), new("Key6", "Value3"));

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.Equal(4, GetNumberOfMetricPoints(exportedItems));
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFirstMetricPoint, 1);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForSecondMetricPoint, 2);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForThirdMetricPoint, 3);
        CheckTagsForNthMetricPoint(exportedItems, expectedTagsForFourthMetricPoint, 4);
        sumReceived = GetLongSum(exportedItems);
        if (exportDelta)
        {
            Assert.Equal(70, sumReceived);
        }
        else
        {
            Assert.Equal(145, sumReceived);
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    public void TestInstrumentDisposal(MetricReaderTemporalityPreference temporality)
    {
        var exportedItems = new List<Metric>();

        var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}.1");
        var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}.2");
        var counter1 = meter1.CreateCounter<long>("counterFromMeter1");
        var counter2 = meter2.CreateCounter<long>("counterFromMeter2");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter1.Name)
            .AddMeter(meter2.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            })
            .Build();

        counter1.Add(10, new KeyValuePair<string, object>("key", "value"));
        counter2.Add(10, new KeyValuePair<string, object>("key", "value"));

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);
        exportedItems.Clear();

        counter1.Add(10, new KeyValuePair<string, object>("key", "value"));
        counter2.Add(10, new KeyValuePair<string, object>("key", "value"));
        meter1.Dispose();

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);
        exportedItems.Clear();

        counter1.Add(10, new KeyValuePair<string, object>("key", "value"));
        counter2.Add(10, new KeyValuePair<string, object>("key", "value"));
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        exportedItems.Clear();

        counter1.Add(10, new KeyValuePair<string, object>("key", "value"));
        counter2.Add(10, new KeyValuePair<string, object>("key", "value"));
        meter2.Dispose();

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        exportedItems.Clear();

        counter1.Add(10, new KeyValuePair<string, object>("key", "value"));
        counter2.Add(10, new KeyValuePair<string, object>("key", "value"));
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Empty(exportedItems);
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    public void TestMetricPointCap(MetricReaderTemporalityPreference temporality)
    {
        var exportedItems = new List<Metric>();

        int MetricPointCount()
        {
            var count = 0;

            foreach (var metric in exportedItems)
            {
                foreach (ref readonly var metricPoint in metric.GetMetricPoints())
                {
                    count++;
                }
            }

            return count;
        }

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}");
        var counterLong = meter.CreateCounter<long>("mycounterCapTest");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            })
            .Build();

        // Make one Add with no tags.
        // as currently we reserve 0th index
        // for no tag point!
        // This may be changed later.
        counterLong.Add(10);
        for (int i = 0; i < MeterProviderBuilderSdk.MaxMetricPointsPerMetricDefault + 1; i++)
        {
            counterLong.Add(10, new KeyValuePair<string, object>("key", "value" + i));
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(MeterProviderBuilderSdk.MaxMetricPointsPerMetricDefault, MetricPointCount());

        exportedItems.Clear();
        counterLong.Add(10);
        for (int i = 0; i < MeterProviderBuilderSdk.MaxMetricPointsPerMetricDefault + 1; i++)
        {
            counterLong.Add(10, new KeyValuePair<string, object>("key", "value" + i));
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(MeterProviderBuilderSdk.MaxMetricPointsPerMetricDefault, MetricPointCount());

        counterLong.Add(10);
        for (int i = 0; i < MeterProviderBuilderSdk.MaxMetricPointsPerMetricDefault + 1; i++)
        {
            counterLong.Add(10, new KeyValuePair<string, object>("key", "value" + i));
        }

        // These updates would be dropped.
        counterLong.Add(10, new KeyValuePair<string, object>("key", "valueA"));
        counterLong.Add(10, new KeyValuePair<string, object>("key", "valueB"));
        counterLong.Add(10, new KeyValuePair<string, object>("key", "valueC"));
        exportedItems.Clear();
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(MeterProviderBuilderSdk.MaxMetricPointsPerMetricDefault, MetricPointCount());
    }

    [Fact]
    public void MultithreadedLongCounterTest()
    {
        this.MultithreadedCounterTest(DeltaLongValueUpdatedByEachCall);
    }

    [Fact]
    public void MultithreadedDoubleCounterTest()
    {
        this.MultithreadedCounterTest(DeltaDoubleValueUpdatedByEachCall);
    }

    [Fact]
    public void MultithreadedLongHistogramTest()
    {
        var expected = new long[16];
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = NumberOfThreads * NumberOfMetricUpdateByEachThread;
        }

        // Metric.DefaultHistogramBounds: 0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000
        var values = new long[] { -1, 1, 6, 20, 40, 60, 80, 200, 300, 600, 800, 1001, 3000, 6000, 8000, 10001 };

        this.MultithreadedHistogramTest(expected, values);
    }

    [Fact]
    public void MultithreadedDoubleHistogramTest()
    {
        var expected = new long[16];
        for (var i = 0; i < expected.Length; i++)
        {
            expected[i] = NumberOfThreads * NumberOfMetricUpdateByEachThread;
        }

        // Metric.DefaultHistogramBounds: 0, 5, 10, 25, 50, 75, 100, 250, 500, 750, 1000, 2500, 5000, 7500, 10000
        var values = new double[] { -1.0, 1.0, 6.0, 20.0, 40.0, 60.0, 80.0, 200.0, 300.0, 600.0, 800.0, 1001.0, 3000.0, 6000.0, 8000.0, 10001.0 };

        this.MultithreadedHistogramTest(expected, values);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.InvalidInstrumentNames), MemberType = typeof(MetricTestData))]
    public void InstrumentWithInvalidNameIsIgnoredTest(string instrumentName)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter("InstrumentWithInvalidNameIsIgnoredTest");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        var counterLong = meter.CreateCounter<long>(instrumentName);
        counterLong.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        // instrument should have been ignored
        // as its name does not comply with the specification
        Assert.Empty(exportedItems);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.ValidInstrumentNames), MemberType = typeof(MetricTestData))]
    public void InstrumentWithValidNameIsExportedTest(string name)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter("InstrumentValidNameIsExportedTest");

        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        var counterLong = meter.CreateCounter<long>(name);
        counterLong.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        // Expecting one metric stream.
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal(name, metric.Name);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SetupSdkProviderWithNoReader(bool hasViews)
    {
        // This test ensures that MeterProviderSdk can be set up without any reader
        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{hasViews}");
        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name);

        if (hasViews)
        {
            meterProviderBuilder.AddView("counter", "renamedCounter");
        }

        using var meterProvider = meterProviderBuilder.Build();

        var counter = meter.CreateCounter<long>("counter");

        counter.Add(10, new KeyValuePair<string, object>("key", "value"));
    }

    [Fact]
    public void UnsupportedMetricInstrument()
    {
        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var exportedItems = new List<Metric>();
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.AddSingleton(this.configuration);
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems)
            .Build();

        using (var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log))
        {
            var counter = meter.CreateCounter<decimal>("counter");
            counter.Add(1);

            // This validates that we log InstrumentIgnored event
            // and not something else.
            Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 33));
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Empty(exportedItems);
    }

    private static void CounterUpdateThread<T>(object obj)
        where T : struct, IComparable
    {
        if (obj is not UpdateThreadArguments<T> arguments)
        {
            throw new Exception("Invalid args");
        }

        var mre = arguments.MreToBlockUpdateThread;
        var mreToEnsureAllThreadsStart = arguments.MreToEnsureAllThreadsStart;
        var counter = arguments.Instrument as Counter<T>;
        var valueToUpdate = arguments.ValuesToRecord[0];
        if (Interlocked.Increment(ref arguments.ThreadsStartedCount) == NumberOfThreads)
        {
            mreToEnsureAllThreadsStart.Set();
        }

        // Wait until signalled to start calling update on aggregator
        mre.WaitOne();

        for (int i = 0; i < NumberOfMetricUpdateByEachThread; i++)
        {
            counter.Add(valueToUpdate, new KeyValuePair<string, object>("verb", "GET"));
        }
    }

    private static void HistogramUpdateThread<T>(object obj)
        where T : struct, IComparable
    {
        if (obj is not UpdateThreadArguments<T> arguments)
        {
            throw new Exception("Invalid args");
        }

        var mre = arguments.MreToBlockUpdateThread;
        var mreToEnsureAllThreadsStart = arguments.MreToEnsureAllThreadsStart;
        var histogram = arguments.Instrument as Histogram<T>;

        if (Interlocked.Increment(ref arguments.ThreadsStartedCount) == NumberOfThreads)
        {
            mreToEnsureAllThreadsStart.Set();
        }

        // Wait until signalled to start calling update on aggregator
        mre.WaitOne();

        for (int i = 0; i < NumberOfMetricUpdateByEachThread; i++)
        {
            for (int j = 0; j < arguments.ValuesToRecord.Length; j++)
            {
                histogram.Record(arguments.ValuesToRecord[j]);
            }
        }
    }

    private void MultithreadedCounterTest<T>(T deltaValueUpdatedByEachCall)
        where T : struct, IComparable
    {
        var metricItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{typeof(T).Name}.{deltaValueUpdatedByEachCall}");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(metricItems)
            .Build();

        var argToThread = new UpdateThreadArguments<T>
        {
            ValuesToRecord = new T[] { deltaValueUpdatedByEachCall },
            Instrument = meter.CreateCounter<T>("counter"),
            MreToBlockUpdateThread = new ManualResetEvent(false),
            MreToEnsureAllThreadsStart = new ManualResetEvent(false),
        };

        Thread[] t = new Thread[NumberOfThreads];
        for (int i = 0; i < NumberOfThreads; i++)
        {
            t[i] = new Thread(CounterUpdateThread<T>);
            t[i].Start(argToThread);
        }

        argToThread.MreToEnsureAllThreadsStart.WaitOne();
        Stopwatch sw = Stopwatch.StartNew();
        argToThread.MreToBlockUpdateThread.Set();

        for (int i = 0; i < NumberOfThreads; i++)
        {
            t[i].Join();
        }

        this.output.WriteLine($"Took {sw.ElapsedMilliseconds} msecs. Total threads: {NumberOfThreads}, each thread doing {NumberOfMetricUpdateByEachThread} recordings.");

        meterProvider.ForceFlush();

        if (typeof(T) == typeof(long))
        {
            var sumReceived = GetLongSum(metricItems);
            var expectedSum = DeltaLongValueUpdatedByEachCall * NumberOfMetricUpdateByEachThread * NumberOfThreads;
            Assert.Equal(expectedSum, sumReceived);
        }
        else if (typeof(T) == typeof(double))
        {
            var sumReceived = GetDoubleSum(metricItems);
            var expectedSum = DeltaDoubleValueUpdatedByEachCall * NumberOfMetricUpdateByEachThread * NumberOfThreads;
            Assert.Equal(expectedSum, sumReceived, 2);
        }
    }

    private void MultithreadedHistogramTest<T>(long[] expected, T[] values)
        where T : struct, IComparable
    {
        var bucketCounts = new long[11];

        var metrics = new List<Metric>();
        var metricReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(metrics));

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{typeof(T).Name}");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddReader(metricReader)
            .Build();

        var argsToThread = new UpdateThreadArguments<T>
        {
            Instrument = meter.CreateHistogram<T>("histogram"),
            MreToBlockUpdateThread = new ManualResetEvent(false),
            MreToEnsureAllThreadsStart = new ManualResetEvent(false),
            ValuesToRecord = values,
        };

        Thread[] t = new Thread[NumberOfThreads];
        for (int i = 0; i < NumberOfThreads; i++)
        {
            t[i] = new Thread(HistogramUpdateThread<T>);
            t[i].Start(argsToThread);
        }

        argsToThread.MreToEnsureAllThreadsStart.WaitOne();
        Stopwatch sw = Stopwatch.StartNew();
        argsToThread.MreToBlockUpdateThread.Set();

        for (int i = 0; i < NumberOfThreads; i++)
        {
            t[i].Join();
        }

        this.output.WriteLine($"Took {sw.ElapsedMilliseconds} msecs. Total threads: {NumberOfThreads}, each thread doing {NumberOfMetricUpdateByEachThread * values.Length} recordings.");

        metricReader.Collect();

        foreach (var metric in metrics)
        {
            foreach (var metricPoint in metric.GetMetricPoints())
            {
                bucketCounts = metricPoint.GetHistogramBuckets().RunningBucketCounts;
            }
        }

        Assert.Equal(expected, bucketCounts);
    }

    private class UpdateThreadArguments<T>
        where T : struct, IComparable
    {
        public ManualResetEvent MreToBlockUpdateThread;
        public ManualResetEvent MreToEnsureAllThreadsStart;
        public int ThreadsStartedCount;
        public Instrument<T> Instrument;
        public T[] ValuesToRecord;
    }
}

public class MetricApiTest : MetricApiTestsBase
{
    public MetricApiTest(ITestOutputHelper output)
        : base(output, emitOverflowAttribute: false, shouldReclaimUnusedMetricPoints: false)
    {
    }
}

public class MetricApiTestWithOverflowAttribute : MetricApiTestsBase
{
    public MetricApiTestWithOverflowAttribute(ITestOutputHelper output)
        : base(output, emitOverflowAttribute: true, shouldReclaimUnusedMetricPoints: false)
    {
    }
}

public class MetricApiTestWithReclaimAttribute : MetricApiTestsBase
{
    public MetricApiTestWithReclaimAttribute(ITestOutputHelper output)
        : base(output, emitOverflowAttribute: false, shouldReclaimUnusedMetricPoints: true)
    {
    }
}

public class MetricApiTestWithBothOverflowAndReclaimAttributes : MetricApiTestsBase
{
    public MetricApiTestWithBothOverflowAndReclaimAttributes(ITestOutputHelper output)
        : base(output, emitOverflowAttribute: true, shouldReclaimUnusedMetricPoints: true)
    {
    }
}
