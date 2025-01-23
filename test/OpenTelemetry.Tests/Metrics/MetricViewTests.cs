// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;
using OpenTelemetry.Internal;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricViewTests : MetricTestsBase
{
    private const int MaxTimeToAllowForFlush = 10000;

    [Fact]
    public void ViewToRenameMetric()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("name1", "renamed")
            .AddInMemoryExporter(exportedItems));

        // Expecting one metric stream.
        var counterLong = meter.CreateCounter<long>("name1");
        counterLong.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal("renamed", metric.Name);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.InvalidInstrumentNames), MemberType = typeof(MetricTestData))]
    public void AddViewWithInvalidNameThrowsArgumentException(string viewNewName)
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter("AddViewWithInvalidNameThrowsArgumentException");

        var ex = Assert.Throws<ArgumentException>(() => this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter1.Name)
            .AddView("name1", viewNewName)
            .AddInMemoryExporter(exportedItems)));

        Assert.Contains($"Custom view name {viewNewName} is invalid.", ex.Message);

        ex = Assert.Throws<ArgumentException>(() => this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter1.Name)
            .AddView("name1", new MetricStreamConfiguration() { Name = viewNewName })
            .AddInMemoryExporter(exportedItems)));

        Assert.Contains($"Custom view name {viewNewName} is invalid.", ex.Message);
    }

    [Fact]
    public void AddViewWithNullMetricStreamConfigurationThrowsArgumentNullException()
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter("AddViewWithInvalidNameThrowsArgumentException");

        Assert.Throws<ArgumentNullException>(() => this.BuildMeterProvider(out var meterProvider, builder => builder
           .AddMeter(meter1.Name)
           .AddView("name1", (MetricStreamConfiguration)null!)
           .AddInMemoryExporter(exportedItems)));
    }

    [Fact]
    public void AddViewWithNameThrowsInvalidArgumentExceptionWhenConflict()
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter("AddViewWithGuaranteedConflictThrowsInvalidArgumentException");

        Assert.Throws<ArgumentException>(() => this.BuildMeterProvider(out var meterProvider, builder => builder
           .AddMeter(meter1.Name)
           .AddView("instrumenta.*", name: "newname")
           .AddInMemoryExporter(exportedItems)));
    }

    [Fact]
    public void AddViewWithNameInMetricStreamConfigurationThrowsInvalidArgumentExceptionWhenConflict()
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter("AddViewWithGuaranteedConflictThrowsInvalidArgumentException");

        Assert.Throws<ArgumentException>(() => this.BuildMeterProvider(out var meterProvider, builder => builder
           .AddMeter(meter1.Name)
           .AddView("instrumenta.*", new MetricStreamConfiguration() { Name = "newname" })
           .AddInMemoryExporter(exportedItems)));
    }

    [Fact]
    public void AddViewWithExceptionInUserCallbackAppliedDefault()
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter("AddViewWithExceptionInUserCallback");
        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
           .AddMeter(meter1.Name)
           .AddView((instrument) => { throw new Exception("bad"); })
           .AddInMemoryExporter(exportedItems));

        using (var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log))
        {
            var counter1 = meter1.CreateCounter<long>("counter1");
            counter1.Add(1);

            var metricViewIgnoredEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 41);
            Assert.Single(metricViewIgnoredEvents);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        // Counter is still reported with default config
        // even if View is ignored due to View exception.
        Assert.Single(exportedItems);
    }

    [Fact]
    public void AddViewWithExceptionInUserCallbackNoDefault()
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter("AddViewWithExceptionInUserCallback");
        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
           .AddMeter(meter1.Name)
           .AddView((instrument) => { throw new Exception("bad"); })
           .AddView("*", MetricStreamConfiguration.Drop)
           .AddInMemoryExporter(exportedItems));

        using (var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log))
        {
            var counter1 = meter1.CreateCounter<long>("counter1");
            counter1.Add(1);
            Assert.Single(inMemoryEventListener.Events.Where((e) => e.EventId == 41));
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        // Counter is not reported.
        // as the View is ignored due to View exception.
        // and Default is suppressed with * -> Drop
        Assert.Empty(exportedItems);
    }

    [Fact]
    public void AddViewsWithAndWithoutExceptionInUserCallback()
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter("AddViewWithExceptionInUserCallback");
        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
           .AddMeter(meter1.Name)
           .AddView((instrument) => { throw new Exception("bad"); })
           .AddView((instrument) => { return new MetricStreamConfiguration() { Name = "newname" }; })
           .AddInMemoryExporter(exportedItems));

        using (var inMemoryEventListener = new InMemoryEventListener(OpenTelemetrySdkEventSource.Log))
        {
            var counter1 = meter1.CreateCounter<long>("counter1");
            counter1.Add(1);

            var metricViewIgnoredEvents = inMemoryEventListener.Events.Where((e) => e.EventId == 41);
            Assert.Single(metricViewIgnoredEvents);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        // Counter is still reported with 2nd View
        // even if 1st View is ignored due to View exception.
        Assert.Single(exportedItems);
        Assert.Equal("newname", exportedItems[0].Name);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.InvalidHistogramBoundaries), MemberType = typeof(MetricTestData))]
    public void AddViewWithInvalidHistogramBoundsThrowsArgumentException(double[] boundaries)
    {
        var ex = Assert.Throws<ArgumentException>(() => this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddView("name1", new ExplicitBucketHistogramConfiguration { Boundaries = boundaries })));

        Assert.Contains("Histogram boundaries must be in ascending order with distinct values", ex.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void AddViewWithInvalidExponentialHistogramMaxSizeConfigThrowsArgumentException(int maxSize)
    {
        var ex = Assert.Throws<ArgumentException>(() => this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddView("name1", new Base2ExponentialBucketHistogramConfiguration { MaxSize = maxSize })));

        Assert.Contains("Histogram max size is invalid", ex.Message);
    }

    [Theory]
    [InlineData(-12)]
    [InlineData(21)]
    public void AddViewWithInvalidExponentialHistogramMaxScaleConfigThrowsArgumentException(int maxScale)
    {
        var ex = Assert.Throws<ArgumentException>(() => this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddView("name1", new Base2ExponentialBucketHistogramConfiguration { MaxScale = maxScale })));

        Assert.Contains("Histogram max scale is invalid", ex.Message);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.InvalidHistogramBoundaries), MemberType = typeof(MetricTestData))]
    public void AddViewWithInvalidHistogramBoundsIgnored(double[] boundaries)
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter("AddViewWithInvalidHistogramBoundsIgnored");

        var counter1 = meter1.CreateCounter<long>("counter1");

        using (var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter1.Name)
            .AddView((instrument) =>
            {
                return instrument.Name == counter1.Name
                    ? new ExplicitBucketHistogramConfiguration() { Boundaries = boundaries }
                    : null;
            })
            .AddInMemoryExporter(exportedItems)))
        {
            counter1.Add(1);
        }

        // Counter is aggregated with default configuration
        // as the View config is ignored due to invalid histogram bounds.
        Assert.Single(exportedItems);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.ValidInstrumentNames), MemberType = typeof(MetricTestData))]
    public void ViewWithValidNameExported(string viewNewName)
    {
        var exportedItems = new List<Metric>();

        using var meter1 = new Meter("ViewWithInvalidNameIgnoredTest");
        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter1.Name)
            .AddView("name1", viewNewName)
            .AddInMemoryExporter(exportedItems));

        var counterLong = meter1.CreateCounter<long>("name1");
        counterLong.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        // Expecting one metric stream.
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal(viewNewName, metric.Name);
    }

    [Fact]
    public void ViewToRenameMetricConditionally()
    {
        using var meter1 = new Meter($"{Utils.GetCurrentMethodName()}.1");
        using var meter2 = new Meter($"{Utils.GetCurrentMethodName()}.2");

        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter1.Name)
            .AddMeter(meter2.Name)
            .AddView((instrument) =>
            {
                if (instrument.Meter.Name.Equals(meter2.Name, StringComparison.OrdinalIgnoreCase)
                    && instrument.Name.Equals("name1", StringComparison.OrdinalIgnoreCase))
                {
                    return new MetricStreamConfiguration() { Name = "name1_Renamed", Description = "new description" };
                }
                else
                {
                    return null;
                }
            })
            .AddInMemoryExporter(exportedItems));

        // Without views only 1 stream would be
        // exported (the 2nd one gets dropped due to
        // name conflict). Due to renaming with Views,
        // we expect 2 metric streams here.
        var counter1 = meter1.CreateCounter<long>("name1", "unit", "original_description");
        var counter2 = meter2.CreateCounter<long>("name1", "unit", "original_description");
        counter1.Add(10);
        counter2.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);
        Assert.Equal("name1", exportedItems[0].Name);
        Assert.Equal("name1_Renamed", exportedItems[1].Name);
        Assert.Equal("original_description", exportedItems[0].Description);
        Assert.Equal("new description", exportedItems[1].Description);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.InvalidInstrumentNames), MemberType = typeof(MetricTestData))]
    public void ViewWithInvalidNameIgnoredConditionally(string viewNewName)
    {
        using var meter1 = new Meter("ViewToRenameMetricConditionallyTest");
        var exportedItems = new List<Metric>();
        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter1.Name)

            // since here it's a func, we can't validate the name right away
            // so the view is allowed to be added, but upon instrument creation it's going to be ignored.
            .AddView((instrument) =>
            {
                if (instrument.Meter.Name.Equals(meter1.Name, StringComparison.OrdinalIgnoreCase)
                    && instrument.Name.Equals("name1", StringComparison.OrdinalIgnoreCase))
                {
                    // invalid instrument name as per the spec
                    return new MetricStreamConfiguration() { Name = viewNewName, Description = "new description" };
                }
                else
                {
                    return null;
                }
            })
            .AddInMemoryExporter(exportedItems));

        // Because the MetricStreamName passed is invalid, the view is ignored,
        // and default aggregation is used.
        var counter1 = meter1.CreateCounter<long>("name1", "unit", "original_description");
        counter1.Add(10);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.Single(exportedItems);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.ValidInstrumentNames), MemberType = typeof(MetricTestData))]
    public void ViewWithValidNameConditionally(string viewNewName)
    {
        using var meter1 = new Meter("ViewToRenameMetricConditionallyTest");
        var exportedItems = new List<Metric>();
        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter1.Name)
            .AddView((instrument) =>
            {
                if (instrument.Meter.Name.Equals(meter1.Name, StringComparison.OrdinalIgnoreCase)
                    && instrument.Name.Equals("name1", StringComparison.OrdinalIgnoreCase))
                {
                    // invalid instrument name as per the spec
                    return new MetricStreamConfiguration() { Name = viewNewName, Description = "new description" };
                }
                else
                {
                    return null;
                }
            })
            .AddInMemoryExporter(exportedItems));

        // Expecting one metric stream.
        var counter1 = meter1.CreateCounter<long>("name1", "unit", "original_description");
        counter1.Add(10);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        // Expecting one metric stream.
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal(viewNewName, metric.Name);
    }

    [Fact]
    public void ViewWithNullCustomNameTakesInstrumentName()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter("ViewToRenameMetricConditionallyTest");

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView((instrument) =>
            {
                if (instrument.Name.Equals("name1", StringComparison.OrdinalIgnoreCase))
                {
                    // null View name
                    return new MetricStreamConfiguration() { };
                }
                else
                {
                    return null;
                }
            })
            .AddInMemoryExporter(exportedItems));

        // Expecting one metric stream.
        // Since the View name was null, the instrument name was used instead
        var counter1 = meter.CreateCounter<long>("name1", "unit", "original_description");
        counter1.Add(10);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        // Expecting one metric stream.
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal(counter1.Name, metric.Name);
    }

    [Fact]
    public void ViewToProduceMultipleStreamsFromInstrument()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("name1", "renamedStream1")
            .AddView("name1", "renamedStream2")
            .AddInMemoryExporter(exportedItems));

        // Expecting two metric stream.
        var counterLong = meter.CreateCounter<long>("name1");
        counterLong.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);
        Assert.Equal("renamedStream1", exportedItems[0].Name);
        Assert.Equal("renamedStream2", exportedItems[1].Name);
    }

    [Fact]
    public void ViewToProduceMultipleStreamsWithDuplicatesFromInstrument()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("name1", "renamedStream1")
            .AddView("name1", "renamedStream2")
            .AddView("name1", "renamedStream2")
            .AddInMemoryExporter(exportedItems));

        // Expecting three metric stream.
        // the second .AddView("name1", "renamedStream2")
        // produces a conflicting metric stream.
        var counterLong = meter.CreateCounter<long>("name1");
        counterLong.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(3, exportedItems.Count);
        Assert.Equal("renamedStream1", exportedItems[0].Name);
        Assert.Equal("renamedStream2", exportedItems[1].Name);
        Assert.Equal("renamedStream2", exportedItems[2].Name);
    }

    [Fact]
    public void ViewWithHistogramConfigurationIgnoredWhenAppliedToNonHistogram()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("NotAHistogram", new ExplicitBucketHistogramConfiguration() { Name = "ImAnExplicitBoundsHistogram" })
            .AddView("NotAHistogram", new Base2ExponentialBucketHistogramConfiguration() { Name = "ImAnExponentialHistogram" })
            .AddInMemoryExporter(exportedItems));

        var counter = meter.CreateCounter<long>("NotAHistogram");
        counter.Add(10);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.Single(exportedItems);
        var metric = exportedItems[0];

        Assert.Equal("NotAHistogram", metric.Name);

        List<MetricPoint> metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);
        var metricPoint = metricPoints[0];
        Assert.Equal(10, metricPoint.GetSumLong());
    }

    [Fact]
    public void ViewToProduceCustomHistogramBound()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();
        var boundaries = new double[] { 10, 20 };

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("MyHistogram", new ExplicitBucketHistogramConfiguration() { Name = "MyHistogramDefaultBound" })
            .AddView("MyHistogram", new ExplicitBucketHistogramConfiguration() { Boundaries = boundaries })
            .AddInMemoryExporter(exportedItems));

        var histogram = meter.CreateHistogram<long>("MyHistogram");
        histogram.Record(-10);
        histogram.Record(0);
        histogram.Record(1);
        histogram.Record(9);
        histogram.Record(10);
        histogram.Record(11);
        histogram.Record(19);
        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);
        var metricDefault = exportedItems[0];
        var metricCustom = exportedItems[1];

        Assert.Equal("MyHistogramDefaultBound", metricDefault.Name);
        Assert.Equal("MyHistogram", metricCustom.Name);

        List<MetricPoint> metricPointsDefault = new List<MetricPoint>();
        foreach (ref readonly var mp in metricDefault.GetMetricPoints())
        {
            metricPointsDefault.Add(mp);
        }

        Assert.Single(metricPointsDefault);
        var histogramPoint = metricPointsDefault[0];

        var count = histogramPoint.GetHistogramCount();
        var sum = histogramPoint.GetHistogramSum();

        Assert.Equal(40, sum);
        Assert.Equal(7, count);

        int index = 0;
        int actualCount = 0;
        var expectedBucketCounts = new long[] { 2, 1, 2, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
        foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
        {
            Assert.Equal(expectedBucketCounts[index], histogramMeasurement.BucketCount);
            index++;
            actualCount++;
        }

        Assert.Equal(Metric.DefaultHistogramBounds.Length + 1, actualCount);

        List<MetricPoint> metricPointsCustom = new List<MetricPoint>();
        foreach (ref readonly var mp in metricCustom.GetMetricPoints())
        {
            metricPointsCustom.Add(mp);
        }

        Assert.Single(metricPointsCustom);
        histogramPoint = metricPointsCustom[0];

        count = histogramPoint.GetHistogramCount();
        sum = histogramPoint.GetHistogramSum();

        Assert.Equal(40, sum);
        Assert.Equal(7, count);

        index = 0;
        actualCount = 0;
        expectedBucketCounts = new long[] { 5, 2, 0 };
        foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
        {
            Assert.Equal(expectedBucketCounts[index], histogramMeasurement.BucketCount);
            index++;
            actualCount++;
        }

        Assert.Equal(boundaries.Length + 1, actualCount);
    }

    [Fact]
    public void HistogramWithAdviceBoundaries_HandlesAllTypes()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();
        int counter = 0;

        using var container = this.BuildMeterProvider(out var meterProvider, builder =>
        {
            builder.AddMeter(meter.Name);
            builder.AddInMemoryExporter(exportedItems);
        });

        // Test cases for different histogram types
        var histograms = new Instrument[]
        {
            meter.CreateHistogram<long>("longHistogram", unit: null, description: null, tags: null, new() { HistogramBucketBoundaries = new List<long>() { 10, 20 } }),
            meter.CreateHistogram<int>("intHistogram", unit: null, description: null, tags: null, new() { HistogramBucketBoundaries = new List<int>() { 10, 20 } }),
            meter.CreateHistogram<short>("shortHistogram", unit: null, description: null, tags: null, new() { HistogramBucketBoundaries = new List<short>() { 10, 20 } }),
            meter.CreateHistogram<float>("floatHistogram", unit: null, description: null, tags: null, new() { HistogramBucketBoundaries = new List<float>() { 10.0F, 20.0F } }),
            meter.CreateHistogram<double>("doubleHistogram", unit: null, description: null, tags: null, new() { HistogramBucketBoundaries = new List<double>() { 10.0, 20.0 } }),
        };

        foreach (var histogram in histograms)
        {
            exportedItems.Clear();

            if (histogram is Histogram<long> longHistogram)
            {
                longHistogram.Record(-10);
                longHistogram.Record(9);
                longHistogram.Record(19);
            }
            else if (histogram is Histogram<int> intHistogram)
            {
                intHistogram.Record(-10);
                intHistogram.Record(9);
                intHistogram.Record(19);
                counter++;
            }
            else if (histogram is Histogram<short> shortHistogram)
            {
                shortHistogram.Record(-10);
                shortHistogram.Record(9);
                shortHistogram.Record(19);
                counter++;
            }
            else if (histogram is Histogram<float> floatHistogram)
            {
                floatHistogram.Record(-10.0F);
                floatHistogram.Record(9.0F);
                floatHistogram.Record(19.0F);
                counter++;
            }
            else if (histogram is Histogram<double> doubleHistogram)
            {
                doubleHistogram.Record(-10.0);
                doubleHistogram.Record(9.0);
                doubleHistogram.Record(19.0);
                counter++;
            }

            meterProvider.ForceFlush(MaxTimeToAllowForFlush);
            var metricCustom = exportedItems[counter];

            List<MetricPoint> metricPointsCustom = new List<MetricPoint>();
            foreach (ref readonly var mp in metricCustom.GetMetricPoints())
            {
                metricPointsCustom.Add(mp);
            }

            Assert.Single(metricPointsCustom);
            var histogramPoint = metricPointsCustom[0];

            var count = histogramPoint.GetHistogramCount();
            var sum = histogramPoint.GetHistogramSum();

            Assert.Equal(18, sum);
            Assert.Equal(3, count);

            var index = 0;
            var actualCount = 0;
            long[] expectedBucketCounts = new long[] { 2, 1, 0 };

            foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
            {
                Assert.Equal(expectedBucketCounts[index], histogramMeasurement.BucketCount);
                index++;
                actualCount++;
            }

            Assert.Equal(3, actualCount);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void HistogramWithAdviceBoundariesSpecifiedTests(bool useViewToOverride)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();
        IReadOnlyList<long> adviceBoundaries = new List<long>() { 5, 10, 20 };
        double[] viewBoundaries = new double[] { 10, 20 };

        using var container = this.BuildMeterProvider(out var meterProvider, builder =>
        {
            builder.AddMeter(meter.Name);

            if (useViewToOverride)
            {
                builder.AddView("MyHistogram", new ExplicitBucketHistogramConfiguration { Boundaries = viewBoundaries });
            }

            builder.AddInMemoryExporter(exportedItems);
        });

        var histogram = meter.CreateHistogram<long>(
            "MyHistogram",
            unit: null,
            description: null,
            tags: null,
            new()
            {
                HistogramBucketBoundaries = adviceBoundaries,
            });

        histogram.Record(-10);
        histogram.Record(0);
        histogram.Record(1);
        histogram.Record(9);
        histogram.Record(10);
        histogram.Record(11);
        histogram.Record(19);
        histogram.Record(22);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metricCustom = exportedItems[0];

        Assert.Equal("MyHistogram", metricCustom.Name);

        List<MetricPoint> metricPointsCustom = new List<MetricPoint>();
        foreach (ref readonly var mp in metricCustom.GetMetricPoints())
        {
            metricPointsCustom.Add(mp);
        }

        Assert.Single(metricPointsCustom);
        var histogramPoint = metricPointsCustom[0];

        var count = histogramPoint.GetHistogramCount();
        var sum = histogramPoint.GetHistogramSum();

        Assert.Equal(62, sum);
        Assert.Equal(8, count);

        var index = 0;
        var actualCount = 0;
        long[] expectedBucketCounts = useViewToOverride ? new long[] { 5, 2, 1 } : new long[] { 3, 2, 2, 1 };

        foreach (var histogramMeasurement in histogramPoint.GetHistogramBuckets())
        {
            Assert.Equal(expectedBucketCounts[index], histogramMeasurement.BucketCount);
            index++;
            actualCount++;
        }

        Assert.Equal(useViewToOverride ? viewBoundaries.Length + 1 : adviceBoundaries.Count + 1, actualCount);
    }

    [Fact]
    public void ViewToProduceExponentialHistogram()
    {
        var valuesToRecord = new[] { -10, 0, 1, 9, 10, 11, 19 };

        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("MyHistogram", new Base2ExponentialBucketHistogramConfiguration())
            .AddInMemoryExporter(exportedItems));

        var histogram = meter.CreateHistogram<long>("MyHistogram");
        var expectedHistogram = new Base2ExponentialBucketHistogram();
        foreach (var value in valuesToRecord)
        {
            histogram.Record(value);

            if (value >= 0)
            {
                expectedHistogram.Record(value);
            }
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];

        Assert.Equal("MyHistogram", metric.Name);

        var metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);
        var metricPoint = metricPoints[0];

        var count = metricPoint.GetHistogramCount();
        var sum = metricPoint.GetHistogramSum();

        AggregatorTests.AssertExponentialBucketsAreCorrect(expectedHistogram, metricPoint.GetExponentialHistogramData());
        Assert.Equal(50, sum);
        Assert.Equal(6, count);
    }

    [Theory]
    [MemberData(nameof(MetricTestData.ValidHistogramMinMax), MemberType = typeof(MetricTestData))]
    public void HistogramMinMax(double[] values, HistogramConfiguration histogramConfiguration, double expectedMin, double expectedMax)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var histogram = meter.CreateHistogram<double>("MyHistogram");
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView(histogram.Name, histogramConfiguration)
            .AddInMemoryExporter(exportedItems));

        for (var i = 0; i < values.Length; i++)
        {
            histogram.Record(values[i]);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        var metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in exportedItems[0].GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        var histogramPoint = metricPoints[0];
        if (histogramPoint.TryGetHistogramMinMaxValues(out double min, out double max))
        {
            Assert.Equal(expectedMin, min);
            Assert.Equal(expectedMax, max);
        }
        else
        {
            Assert.Fail("MinMax expected");
        }
    }

    [Theory]
    [MemberData(nameof(MetricTestData.InvalidHistogramMinMax), MemberType = typeof(MetricTestData))]
    public void HistogramMinMaxNotPresent(double[] values, HistogramConfiguration histogramConfiguration)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var histogram = meter.CreateHistogram<double>("MyHistogram");
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView(histogram.Name, histogramConfiguration)
            .AddInMemoryExporter(exportedItems));

        for (var i = 0; i < values.Length; i++)
        {
            histogram.Record(values[i]);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        var metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in exportedItems[0].GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        var histogramPoint = metricPoints[0];
        Assert.False(histogramPoint.TryGetHistogramMinMaxValues(out double _, out double _));
    }

    [Fact]
    public void ViewToSelectTagKeys()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("FruitCounter", new MetricStreamConfiguration()
            {
                TagKeys = new string[] { "name" },
                Name = "NameOnly",
            })
            .AddView("FruitCounter", new MetricStreamConfiguration()
            {
                TagKeys = new string[] { "size" },
                Name = "SizeOnly",
            })
            .AddView("FruitCounter", new MetricStreamConfiguration()
            {
                TagKeys = Array.Empty<string>(),
                Name = "NoTags",
            })
            .AddInMemoryExporter(exportedItems));

        var counter = meter.CreateCounter<long>("FruitCounter");
        counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "small"));
        counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "small"));

        counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "medium"));
        counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "medium"));

        counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "large"));
        counter.Add(10, new("name", "apple"), new("color", "red"), new("size", "large"));

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(3, exportedItems.Count);
        var metric = exportedItems[0];
        Assert.Equal("NameOnly", metric.Name);
        List<MetricPoint> metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        // Only one point expected "apple"
        Assert.Single(metricPoints);

        metric = exportedItems[1];
        Assert.Equal("SizeOnly", metric.Name);
        metricPoints.Clear();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        // 3 points small,medium,large expected
        Assert.Equal(3, metricPoints.Count);

        metric = exportedItems[2];
        Assert.Equal("NoTags", metric.Name);
        metricPoints.Clear();
        foreach (ref readonly var mp in metric.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        // Single point expected.
        Assert.Single(metricPoints);
    }

    [Fact]
    public void ViewToDropSingleInstrument()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("counterNotInteresting", MetricStreamConfiguration.Drop)
            .AddInMemoryExporter(exportedItems));

        // Expecting one metric stream.
        var counterInteresting = meter.CreateCounter<long>("counterInteresting");
        var counterNotInteresting = meter.CreateCounter<long>("counterNotInteresting");
        counterInteresting.Add(10);
        counterNotInteresting.Add(10);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal("counterInteresting", metric.Name);
    }

    [Fact]
    public void ViewToDropSingleInstrumentObservableCounter()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("observableCounterNotInteresting", MetricStreamConfiguration.Drop)
            .AddInMemoryExporter(exportedItems));

        // Expecting one metric stream.
        meter.CreateObservableCounter("observableCounterNotInteresting", () => { return 10; }, "ms");
        meter.CreateObservableCounter("observableCounterInteresting", () => { return 10; }, "ms");

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal("observableCounterInteresting", metric.Name);
    }

    [Fact]
    public void ViewToDropSingleInstrumentObservableGauge()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("observableGaugeNotInteresting", MetricStreamConfiguration.Drop)
            .AddInMemoryExporter(exportedItems));

        // Expecting one metric stream.
        meter.CreateObservableGauge("observableGaugeNotInteresting", () => { return 10; }, "ms");
        meter.CreateObservableGauge("observableGaugeInteresting", () => { return 10; }, "ms");

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        var metric = exportedItems[0];
        Assert.Equal("observableGaugeInteresting", metric.Name);
    }

    [Fact]
    public void ViewToDropMultipleInstruments()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("server*", MetricStreamConfiguration.Drop)
            .AddInMemoryExporter(exportedItems));

        // Expecting two client metric streams as both server* are dropped.
        var serverRequests = meter.CreateCounter<long>("server.requests");
        var serverExceptions = meter.CreateCounter<long>("server.exceptions");
        var clientRequests = meter.CreateCounter<long>("client.requests");
        var clientExceptions = meter.CreateCounter<long>("client.exceptions");
        serverRequests.Add(10);
        serverExceptions.Add(10);
        clientRequests.Add(10);
        clientExceptions.Add(10);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);
        Assert.Equal("client.requests", exportedItems[0].Name);
        Assert.Equal("client.exceptions", exportedItems[1].Name);
    }

    [Fact]
    public void ViewToDropAndRetainInstrument()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("server.requests", MetricStreamConfiguration.Drop)
            .AddView("server.requests", "server.request_renamed")
            .AddInMemoryExporter(exportedItems));

        // Expecting one metric stream even though a View is asking
        // to drop the instrument, because another View is matching
        // the instrument, which asks to aggregate with defaults
        // and a use a new name for the resulting metric.
        var serverRequests = meter.CreateCounter<long>("server.requests");
        serverRequests.Add(10);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Single(exportedItems);
        Assert.Equal("server.request_renamed", exportedItems[0].Name);
    }

    [Fact]
    public void ViewConflict_OneInstrument_DifferentDescription()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter({Utils.GetCurrentMethodName());

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView("instrumentName", new MetricStreamConfiguration() { Description = "newDescription1" })
            .AddView("instrumentName", new MetricStreamConfiguration() { Description = "newDescription2" })
            .AddInMemoryExporter(exportedItems));

        var instrument = meter.CreateCounter<long>("instrumentName", "instrumentUnit", "instrumentDescription");

        instrument.Add(10);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(2, exportedItems.Count);

        var metric1 = exportedItems[0];
        var metric2 = exportedItems[1];
        Assert.Equal("newDescription1", metric1.Description);
        Assert.Equal("newDescription2", metric2.Description);

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
        Assert.Equal(10, metricPoint2.GetSumLong());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CardinalityLimitOfMatchingViewTakesPrecedenceOverMeterProvider(bool setDefault)
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exportedItems = new List<Metric>();

        using var container = this.BuildMeterProvider(out var meterProvider, builder =>
        {
            if (setDefault)
            {
#pragma warning disable CS0618 // Type or member is obsolete
                builder.SetMaxMetricPointsPerMetricStream(3);
#pragma warning restore CS0618 // Type or member is obsolete
            }

            builder
                .AddMeter(meter.Name)
                .AddView((instrument) =>
                {
                    if (instrument.Name == "counter2")
                    {
                        return new MetricStreamConfiguration() { Name = "MetricStreamA", CardinalityLimit = 10000 };
                    }

                    return null;
                })
                .AddInMemoryExporter(exportedItems);
        });

        var counter1 = meter.CreateCounter<long>("counter1");
        counter1.Add(100);

        var counter2 = meter.CreateCounter<long>("counter2");
        counter2.Add(100);

        var counter3 = meter.CreateCounter<long>("counter3");
        counter3.Add(100);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.Equal(3, exportedItems.Count);

        Assert.Equal(10002, exportedItems[1].AggregatorStore.NumberOfMetricPoints);
        if (setDefault)
        {
            Assert.Equal(5, exportedItems[0].AggregatorStore.NumberOfMetricPoints);
            Assert.Equal(5, exportedItems[2].AggregatorStore.NumberOfMetricPoints);
        }
        else
        {
            Assert.Equal(2002, exportedItems[0].AggregatorStore.NumberOfMetricPoints);
            Assert.Equal(2002, exportedItems[2].AggregatorStore.NumberOfMetricPoints);
        }
    }

    [Fact]
    public void ViewConflict_TwoDistinctInstruments_ThreeStreams()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter({Utils.GetCurrentMethodName());

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView((instrument) =>
            {
                return new MetricStreamConfiguration() { Name = "MetricStreamA", Description = "description", CardinalityLimit = 256 };
            })
            .AddView((instrument) =>
            {
                return instrument.Description == "description1"
                    ? new MetricStreamConfiguration() { Name = "MetricStreamB", CardinalityLimit = 3 }
                    : new MetricStreamConfiguration() { Name = "MetricStreamC", CardinalityLimit = 200000 };
            })
            .AddView((instrument) =>
            {
                // This view is ignored as the passed in CardinalityLimit is out of range.
                return new MetricStreamConfiguration() { Name = "MetricStreamD", CardinalityLimit = -1 };
            })
            .AddInMemoryExporter(exportedItems));

        var instrument1 = meter.CreateCounter<long>("name", "unit", "description1");
        var instrument2 = meter.CreateCounter<long>("name", "unit", "description2");

        instrument1.Add(10);
        instrument2.Add(10);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        Assert.Equal(3, exportedItems.Count);

        var metricA = exportedItems[0];
        var metricB = exportedItems[1];
        var metricC = exportedItems[2];

        Assert.Equal(258, metricA.AggregatorStore.NumberOfMetricPoints);
        Assert.Equal("MetricStreamA", metricA.Name);
        Assert.Equal(20, GetAggregatedValue(metricA));

        Assert.Equal(5, metricB.AggregatorStore.NumberOfMetricPoints);
        Assert.Equal("MetricStreamB", metricB.Name);
        Assert.Equal(10, GetAggregatedValue(metricB));

        Assert.Equal(200002, metricC.AggregatorStore.NumberOfMetricPoints);
        Assert.Equal("MetricStreamC", metricC.Name);
        Assert.Equal(10, GetAggregatedValue(metricC));

        long GetAggregatedValue(Metric metric)
        {
            var metricPoints = new List<MetricPoint>();
            foreach (ref readonly var mp in metric.GetMetricPoints())
            {
                metricPoints.Add(mp);
            }

            Assert.Single(metricPoints);
            return metricPoints[0].GetSumLong();
        }
    }

    [Fact]
    public void ViewConflict_TwoIdenticalInstruments_TwoViews_DifferentTags()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter({Utils.GetCurrentMethodName());

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView((instrument) =>
            {
                return new MetricStreamConfiguration { TagKeys = new[] { "key1" } };
            })
            .AddView((instrument) =>
            {
                return new MetricStreamConfiguration { TagKeys = new[] { "key2" } };
            })
            .AddInMemoryExporter(exportedItems));

        var instrument1 = meter.CreateCounter<long>("name");
        var instrument2 = meter.CreateCounter<long>("name");

        var tags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value"),
            new("key2", "value"),
        };

        instrument1.Add(10, tags);
        instrument2.Add(10, tags);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.Equal(2, exportedItems.Count);
        var metric1 = new List<Metric>() { exportedItems[0] };
        var metric2 = new List<Metric>() { exportedItems[1] };
        var tag1 = new List<KeyValuePair<string, object?>> { tags[0] };
        var tag2 = new List<KeyValuePair<string, object?>> { tags[1] };

        Assert.Equal("name", exportedItems[0].Name);
        Assert.Equal("name", exportedItems[1].Name);
        Assert.Equal(20, GetLongSum(metric1));
        Assert.Equal(20, GetLongSum(metric2));
        CheckTagsForNthMetricPoint(metric1, tag1, 1);
        CheckTagsForNthMetricPoint(metric2, tag2, 1);
    }

    [Fact]
    public void ViewConflict_TwoIdenticalInstruments_TwoViews_SameTags()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter({Utils.GetCurrentMethodName());

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView((instrument) =>
            {
                return new MetricStreamConfiguration { TagKeys = new[] { "key1" } };
            })
            .AddView((instrument) =>
            {
                return new MetricStreamConfiguration { TagKeys = new[] { "key1" } };
            })
            .AddInMemoryExporter(exportedItems));

        var instrument1 = meter.CreateCounter<long>("name");
        var instrument2 = meter.CreateCounter<long>("name");

        var tags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value"),
            new("key2", "value"),
        };

        instrument1.Add(10, tags);
        instrument2.Add(10, tags);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.Equal(2, exportedItems.Count);

        var metric1 = new List<Metric>() { exportedItems[0] };
        var tag1 = new List<KeyValuePair<string, object?>> { tags[0] };
        Assert.Equal("name", exportedItems[0].Name);
        Assert.Equal(20, GetLongSum(metric1));
        CheckTagsForNthMetricPoint(metric1, tag1, 1);

        var metric2 = new List<Metric>() { exportedItems[1] };
        var tag2 = new List<KeyValuePair<string, object?>> { tags[0] };
        Assert.Equal("name", exportedItems[1].Name);
        Assert.Equal(20, GetLongSum(metric2));
        CheckTagsForNthMetricPoint(metric2, tag2, 1);
    }

    [Fact]
    public void ViewConflict_TwoIdenticalInstruments_TwoViews_DifferentHistogramBounds()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter({Utils.GetCurrentMethodName());

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView((instrument) =>
            {
                return new ExplicitBucketHistogramConfiguration { Boundaries = new[] { 5.0, 10.0 } };
            })
            .AddView((instrument) =>
            {
                return new ExplicitBucketHistogramConfiguration { Boundaries = new[] { 10.0, 20.0 } };
            })
            .AddInMemoryExporter(exportedItems));

        var instrument1 = meter.CreateHistogram<long>("name");
        var instrument2 = meter.CreateHistogram<long>("name");

        instrument1.Record(15);
        instrument2.Record(15);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.Equal(2, exportedItems.Count);
        var metric1 = exportedItems[0];
        var metric2 = exportedItems[1];

        Assert.Equal("name", exportedItems[0].Name);
        Assert.Equal("name", exportedItems[1].Name);

        var metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric1.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);
        var metricPoint = metricPoints[0];
        Assert.Equal(2, metricPoint.GetHistogramCount());
        Assert.Equal(30, metricPoint.GetHistogramSum());

        var index = 0;
        var actualCount = 0;
        var expectedBucketCounts = new long[] { 0, 0, 2 };
        foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
        {
            Assert.Equal(expectedBucketCounts[index], histogramMeasurement.BucketCount);
            index++;
            actualCount++;
        }

        metricPoints = new List<MetricPoint>();
        foreach (ref readonly var mp in metric2.GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);
        metricPoint = metricPoints[0];
        Assert.Equal(2, metricPoint.GetHistogramCount());
        Assert.Equal(30, metricPoint.GetHistogramSum());

        index = 0;
        actualCount = 0;
        expectedBucketCounts = new long[] { 0, 2, 0 };
        foreach (var histogramMeasurement in metricPoint.GetHistogramBuckets())
        {
            Assert.Equal(expectedBucketCounts[index], histogramMeasurement.BucketCount);
            index++;
            actualCount++;
        }
    }

    [Fact]
    public void ViewConflict_TwoInstruments_OneMatchesView()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter({Utils.GetCurrentMethodName());

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView((instrument) =>
            {
                if (instrument.Name == "name")
                {
                    return new MetricStreamConfiguration { Name = "othername", TagKeys = new[] { "key1" } };
                }
                else
                {
                    return null;
                }
            })
            .AddInMemoryExporter(exportedItems));

        var instrument1 = meter.CreateCounter<long>("name");
        var instrument2 = meter.CreateCounter<long>("othername");

        var tags = new KeyValuePair<string, object?>[]
        {
            new("key1", "value"),
            new("key2", "value"),
        };

        instrument1.Add(10, tags);
        instrument2.Add(10, tags);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.Equal(2, exportedItems.Count);
        var metric1 = new List<Metric>() { exportedItems[0] };
        var metric2 = new List<Metric>() { exportedItems[1] };

        var tags1 = new List<KeyValuePair<string, object?>> { tags[0] };
        var tags2 = new List<KeyValuePair<string, object?>> { tags[0], tags[1] };

        Assert.Equal("othername", exportedItems[0].Name);
        Assert.Equal("othername", exportedItems[1].Name);

        Assert.Equal(10, GetLongSum(metric1));
        Assert.Equal(10, GetLongSum(metric2));

        CheckTagsForNthMetricPoint(metric1, tags1, 1);
        CheckTagsForNthMetricPoint(metric2, tags2, 1);
    }

    [Fact]
    public void ViewConflict_TwoInstruments_ConflictAvoidedBecauseSecondInstrumentIsDropped()
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter({Utils.GetCurrentMethodName());

        using var container = this.BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .AddView((instrument) =>
            {
                if (instrument.Name == "name")
                {
                    return new MetricStreamConfiguration { Name = "othername" };
                }
                else
                {
                    return MetricStreamConfiguration.Drop;
                }
            })
            .AddInMemoryExporter(exportedItems));

        var instrument1 = meter.CreateCounter<long>("name");
        var instrument2 = meter.CreateCounter<long>("othername");

        instrument1.Add(10);
        instrument2.Add(20);

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        Assert.Single(exportedItems);
        var metric1 = new List<Metric>() { exportedItems[0] };

        Assert.Equal("othername", exportedItems[0].Name);
        Assert.Equal(10, GetLongSum(metric1));
    }
}
