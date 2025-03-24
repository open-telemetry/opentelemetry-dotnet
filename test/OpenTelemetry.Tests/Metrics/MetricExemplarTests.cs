// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MetricExemplarTests : MetricTestsBase
{
    private const int MaxTimeToAllowForFlush = 10000;

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, "always_off", (int)ExemplarFilterType.AlwaysOff)]
    [InlineData(null, "ALWays_ON", (int)ExemplarFilterType.AlwaysOn)]
    [InlineData(null, "trace_based", (int)ExemplarFilterType.TraceBased)]
    [InlineData(null, "invalid", null)]
    [InlineData((int)ExemplarFilterType.AlwaysOn, "trace_based", (int)ExemplarFilterType.AlwaysOn)]
    public void TestExemplarFilterSetFromConfiguration(
        int? programmaticValue,
        string? configValue,
        int? expectedValue)
    {
        var configBuilder = new ConfigurationBuilder();
        if (!string.IsNullOrEmpty(configValue))
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [MeterProviderSdk.ExemplarFilterConfigKey] = configValue,
                [MeterProviderSdk.ExemplarFilterHistogramsConfigKey] = configValue,
            });
        }

        using var container = BuildMeterProvider(out var meterProvider, b =>
        {
            b.ConfigureServices(
                s => s.AddSingleton<IConfiguration>(configBuilder.Build()));

            if (programmaticValue.HasValue)
            {
                b.SetExemplarFilter(((ExemplarFilterType?)programmaticValue).Value);
            }
        });

        var meterProviderSdk = meterProvider as MeterProviderSdk;

        Assert.NotNull(meterProviderSdk);
        Assert.Equal((ExemplarFilterType?)expectedValue, meterProviderSdk.ExemplarFilter);
        if (programmaticValue.HasValue)
        {
            Assert.False(meterProviderSdk.ExemplarFilterForHistograms.HasValue);
        }
        else
        {
            Assert.Equal((ExemplarFilterType?)expectedValue, meterProviderSdk.ExemplarFilterForHistograms);
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    public void TestExemplarsCounter(MetricReaderTemporalityPreference temporality)
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        var counterDouble = meter.CreateCounter<double>("testCounterDouble");
        var counterLong = meter.CreateCounter<long>("testCounterLong");

        using var container = BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(i =>
            {
                if (i.Name.StartsWith("testCounter", StringComparison.Ordinal))
                {
                    return new MetricStreamConfiguration
                    {
                        ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                    };
                }

                return null;
            })
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            }));

        var measurementValues = GenerateRandomValues(2, false, null);
        foreach (var value in measurementValues)
        {
            counterDouble.Add(value.Value);
            counterLong.Add((long)value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        ValidateFirstPhase("testCounterDouble", testStartTime, exportedItems, measurementValues, e => e.DoubleValue);
        ValidateFirstPhase("testCounterLong", testStartTime, exportedItems, measurementValues, e => e.LongValue);

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        var secondMeasurementValues = GenerateRandomValues(1, true, measurementValues);
        foreach (var value in secondMeasurementValues)
        {
            using var act = new Activity("test").Start();
            counterDouble.Add(value.Value);
            counterLong.Add((long)value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        ValidateSecondPhase("testCounterDouble", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues, e => e.DoubleValue);
        ValidateSecondPhase("testCounterLong", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues, e => e.LongValue);

        void ValidateFirstPhase(
            string instrumentName,
            DateTime testStartTime,
            List<Metric> exportedItems,
            (double Value, bool ExpectTraceId)[] measurementValues,
            Func<Exemplar, double> getExemplarValueFunc)
        {
            var metricPoint = GetFirstMetricPoint(exportedItems.Where(m => m.Name == instrumentName));

            Assert.NotNull(metricPoint);
            Assert.True(metricPoint.Value.StartTime >= testStartTime);
            Assert.True(metricPoint.Value.EndTime != default);

            var exemplars = GetExemplars(metricPoint.Value);

            ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, getExemplarValueFunc);
        }

        void ValidateSecondPhase(
            string instrumentName,
            MetricReaderTemporalityPreference temporality,
            DateTime testStartTime,
            List<Metric> exportedItems,
            (double Value, bool ExpectTraceId)[] firstMeasurementValues,
            (double Value, bool ExpectTraceId)[] secondMeasurementValues,
            Func<Exemplar, double> getExemplarValueFunc)
        {
            var metricPoint = GetFirstMetricPoint(exportedItems.Where(m => m.Name == instrumentName));

            Assert.NotNull(metricPoint);
            Assert.True(metricPoint.Value.StartTime >= testStartTime);
            Assert.True(metricPoint.Value.EndTime != default);

            var exemplars = GetExemplars(metricPoint.Value);

            if (temporality == MetricReaderTemporalityPreference.Cumulative)
            {
                // Current design:
                //  First collect we saw Exemplar A & B
                //  Second collect we saw Exemplar C but B remained in the reservoir
                Assert.Equal(2, exemplars.Count);
                secondMeasurementValues = secondMeasurementValues.Concat(firstMeasurementValues.Skip(1).Take(1)).ToArray();
            }
            else
            {
                Assert.Single(exemplars);
            }

            ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, secondMeasurementValues, getExemplarValueFunc);
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    public void TestExemplarsObservable(MetricReaderTemporalityPreference temporality)
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        (double Value, bool ExpectTraceId)[] measurementValues =
        [
            (18D, false),
            (19D, false)
        ];

        int measurementIndex = 0;

        using var meter = new Meter(Utils.GetCurrentMethodName());
        var gaugeDouble = meter.CreateObservableGauge("testGaugeDouble", () => measurementValues[measurementIndex].Value);
        var gaugeLong = meter.CreateObservableGauge("testGaugeLong", () => (long)measurementValues[measurementIndex].Value);
        var counterDouble = meter.CreateObservableCounter("counterDouble", () => measurementValues[measurementIndex].Value);
        var counterLong = meter.CreateObservableCounter("counterLong", () => (long)measurementValues[measurementIndex].Value);

        using var container = BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            }));

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        ValidateFirstPhase("testGaugeDouble", testStartTime, exportedItems, measurementValues, e => e.DoubleValue);
        ValidateFirstPhase("testGaugeLong", testStartTime, exportedItems, measurementValues, e => e.LongValue);
        ValidateFirstPhase("counterDouble", testStartTime, exportedItems, measurementValues, e => e.DoubleValue);
        ValidateFirstPhase("counterLong", testStartTime, exportedItems, measurementValues, e => e.LongValue);

        exportedItems.Clear();

        measurementIndex++;

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        ValidateSecondPhase("testGaugeDouble", testStartTime, exportedItems, measurementValues, e => e.DoubleValue);
        ValidateSecondPhase("testGaugeLong", testStartTime, exportedItems, measurementValues, e => e.LongValue);

        void ValidateFirstPhase(
            string instrumentName,
            DateTime testStartTime,
            List<Metric> exportedItems,
            (double Value, bool ExpectTraceId)[] measurementValues,
            Func<Exemplar, double> getExemplarValueFunc)
        {
            var metricPoint = GetFirstMetricPoint(exportedItems.Where(m => m.Name == instrumentName));
            Assert.NotNull(metricPoint);
            Assert.True(metricPoint.Value.StartTime >= testStartTime);
            Assert.True(metricPoint.Value.EndTime != default);

            var exemplars = GetExemplars(metricPoint.Value);
            ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues.Take(1), getExemplarValueFunc);
        }

        static void ValidateSecondPhase(
            string instrumentName,
            DateTime testStartTime,
            List<Metric> exportedItems,
            (double Value, bool ExpectTraceId)[] measurementValues,
            Func<Exemplar, double> getExemplarValueFunc)
        {
            var metricPoint = GetFirstMetricPoint(exportedItems.Where(m => m.Name == instrumentName));

            Assert.NotNull(metricPoint);
            Assert.True(metricPoint.Value.StartTime >= testStartTime);
            Assert.True(metricPoint.Value.EndTime != default);

            var exemplars = GetExemplars(metricPoint.Value);

            // Note: Gauges are only observed when collection happens. For
            // Cumulative & Delta the behavior will be the same. We will record the
            // single measurement each time as the only exemplar.

            Assert.Single(exemplars);
            ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues.Skip(1), getExemplarValueFunc);
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, null)]
    [InlineData(MetricReaderTemporalityPreference.Delta, null)]
    [InlineData(MetricReaderTemporalityPreference.Delta, "always_on")]
    public void TestExemplarsHistogramWithBuckets(MetricReaderTemporalityPreference temporality, string? configValue)
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        var histogramWithBucketsAndMinMaxDouble = meter.CreateHistogram<double>("histogramWithBucketsAndMinMaxDouble");
        var histogramWithBucketsDouble = meter.CreateHistogram<double>("histogramWithBucketsDouble");
        var histogramWithBucketsAndMinMaxLong = meter.CreateHistogram<long>("histogramWithBucketsAndMinMaxLong");
        var histogramWithBucketsLong = meter.CreateHistogram<long>("histogramWithBucketsLong");

        var buckets = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        var configBuilder = new ConfigurationBuilder();
        if (!string.IsNullOrEmpty(configValue))
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                [MeterProviderSdk.ExemplarFilterConfigKey] = "always_off",
                [MeterProviderSdk.ExemplarFilterHistogramsConfigKey] = configValue,
            });
        }

        using var container = BuildMeterProvider(out var meterProvider, builder =>
        {
            if (string.IsNullOrEmpty(configValue))
            {
                builder.SetExemplarFilter(ExemplarFilterType.AlwaysOn);
            }

            builder
                .ConfigureServices(s => s.AddSingleton<IConfiguration>(configBuilder.Build()))
                .AddMeter(meter.Name)
                .AddView(i =>
                {
                    if (i.Name.StartsWith("histogramWithBucketsAndMinMax", StringComparison.Ordinal))
                    {
                        return new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = buckets,
                        };
                    }
                    else
                    {
                        return new ExplicitBucketHistogramConfiguration
                        {
                            Boundaries = buckets,
                            RecordMinMax = false,
                        };
                    }
                })
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.TemporalityPreference = temporality;
                });
        });

        var measurementValues = buckets
            /* 2000 is here to test overflow measurement */
            .Concat([2000.0])
            .Select(b => (Value: b, ExpectTraceId: false))
            .ToArray();
        foreach (var value in measurementValues)
        {
            histogramWithBucketsAndMinMaxDouble.Record(value.Value);
            histogramWithBucketsDouble.Record(value.Value);
            histogramWithBucketsAndMinMaxLong.Record((long)value.Value);
            histogramWithBucketsLong.Record((long)value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        ValidateFirstPhase("histogramWithBucketsAndMinMaxDouble", testStartTime, exportedItems, measurementValues);
        ValidateFirstPhase("histogramWithBucketsDouble", testStartTime, exportedItems, measurementValues);
        ValidateFirstPhase("histogramWithBucketsAndMinMaxLong", testStartTime, exportedItems, measurementValues);
        ValidateFirstPhase("histogramWithBucketsLong", testStartTime, exportedItems, measurementValues);

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        var secondMeasurementValues = buckets.Take(1).Select(b => (Value: b, ExpectTraceId: true)).ToArray();
        foreach (var value in secondMeasurementValues)
        {
            using var act = new Activity("test").Start();
            histogramWithBucketsAndMinMaxDouble.Record(value.Value);
            histogramWithBucketsDouble.Record(value.Value);
            histogramWithBucketsAndMinMaxLong.Record((long)value.Value);
            histogramWithBucketsLong.Record((long)value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        ValidateScondPhase("histogramWithBucketsAndMinMaxDouble", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);
        ValidateScondPhase("histogramWithBucketsDouble", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);
        ValidateScondPhase("histogramWithBucketsAndMinMaxLong", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);
        ValidateScondPhase("histogramWithBucketsLong", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);

        static void ValidateFirstPhase(
            string instrumentName,
            DateTime testStartTime,
            List<Metric> exportedItems,
            (double Value, bool ExpectTraceId)[] measurementValues)
        {
            var metricPoint = GetFirstMetricPoint(exportedItems.Where(n => n.Name == instrumentName));

            Assert.NotNull(metricPoint);
            Assert.True(metricPoint.Value.StartTime >= testStartTime);
            Assert.True(metricPoint.Value.EndTime != default);

            var exemplars = GetExemplars(metricPoint.Value);

            ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, e => e.DoubleValue);
        }

        static void ValidateScondPhase(
            string instrumentName,
            MetricReaderTemporalityPreference temporality,
            DateTime testStartTime,
            List<Metric> exportedItems,
            (double Value, bool ExpectTraceId)[] firstMeasurementValues,
            (double Value, bool ExpectTraceId)[] secondMeasurementValues)
        {
            var metricPoint = GetFirstMetricPoint(exportedItems.Where(n => n.Name == instrumentName));

            Assert.NotNull(metricPoint);
            Assert.True(metricPoint.Value.StartTime >= testStartTime);
            Assert.True(metricPoint.Value.EndTime != default);

            var exemplars = GetExemplars(metricPoint.Value);

            if (temporality == MetricReaderTemporalityPreference.Cumulative)
            {
                Assert.Equal(11, exemplars.Count);
                secondMeasurementValues = secondMeasurementValues.Concat(firstMeasurementValues.Skip(1)).ToArray();
            }
            else
            {
                Assert.Single(exemplars);
            }

            ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, secondMeasurementValues, e => e.DoubleValue);
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    public void TestExemplarsHistogramWithoutBuckets(MetricReaderTemporalityPreference temporality)
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        var histogramWithoutBucketsAndMinMaxDouble = meter.CreateHistogram<double>("histogramWithoutBucketsAndMinMaxDouble");
        var histogramWithoutBucketsDouble = meter.CreateHistogram<double>("histogramWithoutBucketsDouble");
        var histogramWithoutBucketsAndMinMaxLong = meter.CreateHistogram<long>("histogramWithoutBucketsAndMinMaxLong");
        var histogramWithoutBucketsLong = meter.CreateHistogram<long>("histogramWithoutBucketsLong");

        using var container = BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(i =>
            {
                if (i.Name.StartsWith("histogramWithoutBucketsAndMinMax", StringComparison.Ordinal))
                {
                    return new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [],
                        ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                    };
                }
                else
                {
                    return new ExplicitBucketHistogramConfiguration
                    {
                        Boundaries = [],
                        RecordMinMax = false,
                        ExemplarReservoirFactory = () => new SimpleFixedSizeExemplarReservoir(3),
                    };
                }
            })
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            }));

        var measurementValues = GenerateRandomValues(2, false, null);
        foreach (var value in measurementValues)
        {
            histogramWithoutBucketsAndMinMaxDouble.Record(value.Value);
            histogramWithoutBucketsDouble.Record(value.Value);
            histogramWithoutBucketsAndMinMaxLong.Record((long)value.Value);
            histogramWithoutBucketsLong.Record((long)value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        ValidateFirstPhase("histogramWithoutBucketsAndMinMaxDouble", testStartTime, exportedItems, measurementValues);
        ValidateFirstPhase("histogramWithoutBucketsDouble", testStartTime, exportedItems, measurementValues);
        ValidateFirstPhase("histogramWithoutBucketsAndMinMaxLong", testStartTime, exportedItems, measurementValues);
        ValidateFirstPhase("histogramWithoutBucketsLong", testStartTime, exportedItems, measurementValues);

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        var secondMeasurementValues = GenerateRandomValues(1, true, measurementValues);
        foreach (var value in secondMeasurementValues)
        {
            using var act = new Activity("test").Start();
            histogramWithoutBucketsAndMinMaxDouble.Record(value.Value);
            histogramWithoutBucketsDouble.Record(value.Value);
            histogramWithoutBucketsAndMinMaxLong.Record((long)value.Value);
            histogramWithoutBucketsLong.Record((long)value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        ValidateSecondPhase("histogramWithoutBucketsAndMinMaxDouble", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);
        ValidateSecondPhase("histogramWithoutBucketsDouble", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);
        ValidateSecondPhase("histogramWithoutBucketsAndMinMaxLong", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);
        ValidateSecondPhase("histogramWithoutBucketsLong", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);

        static void ValidateFirstPhase(
            string instrumentName,
            DateTime testStartTime,
            List<Metric> exportedItems,
            (double Value, bool ExpectTraceId)[] measurementValues)
        {
            var metricPoint = GetFirstMetricPoint(exportedItems.Where(n => n.Name == instrumentName));

            Assert.NotNull(metricPoint);
            Assert.True(metricPoint.Value.StartTime >= testStartTime);
            Assert.True(metricPoint.Value.EndTime != default);

            var exemplars = GetExemplars(metricPoint.Value);

            ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, e => e.DoubleValue);
        }

        static void ValidateSecondPhase(
            string instrumentName,
            MetricReaderTemporalityPreference temporality,
            DateTime testStartTime,
            List<Metric> exportedItems,
            (double Value, bool ExpectTraceId)[] firstMeasurementValues,
            (double Value, bool ExpectTraceId)[] secondMeasurementValues)
        {
            var metricPoint = GetFirstMetricPoint(exportedItems.Where(m => m.Name == instrumentName));

            Assert.NotNull(metricPoint);
            Assert.True(metricPoint.Value.StartTime >= testStartTime);
            Assert.True(metricPoint.Value.EndTime != default);

            var exemplars = GetExemplars(metricPoint.Value);

            if (temporality == MetricReaderTemporalityPreference.Cumulative)
            {
                Assert.Equal(2, exemplars.Count);
                secondMeasurementValues = secondMeasurementValues.Concat(firstMeasurementValues.Skip(1)).ToArray();
            }
            else
            {
                Assert.Single(exemplars);
            }

            ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, secondMeasurementValues, e => e.DoubleValue);
        }
    }

    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Cumulative)]
    [InlineData(MetricReaderTemporalityPreference.Delta)]
    public void TestExemplarsExponentialHistogram(MetricReaderTemporalityPreference temporality)
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());
        var exponentialHistogramWithMinMaxDouble = meter.CreateHistogram<double>("exponentialHistogramWithMinMaxDouble");
        var exponentialHistogramDouble = meter.CreateHistogram<double>("exponentialHistogramDouble");
        var exponentialHistogramWithMinMaxLong = meter.CreateHistogram<long>("exponentialHistogramWithMinMaxLong");
        var exponentialHistogramLong = meter.CreateHistogram<long>("exponentialHistogramLong");

        using var container = BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(i =>
            {
                if (i.Name.StartsWith("exponentialHistogramWithMinMax", StringComparison.Ordinal))
                {
                    return new Base2ExponentialBucketHistogramConfiguration();
                }
                else
                {
                    return new Base2ExponentialBucketHistogramConfiguration()
                    {
                        RecordMinMax = false,
                    };
                }
            })
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = temporality;
            }));

        var measurementValues = GenerateRandomValues(20, false, null);
        foreach (var value in measurementValues)
        {
            exponentialHistogramWithMinMaxDouble.Record(value.Value);
            exponentialHistogramDouble.Record(value.Value);
            exponentialHistogramWithMinMaxLong.Record((long)value.Value);
            exponentialHistogramLong.Record((long)value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        ValidateFirstPhase("exponentialHistogramWithMinMaxDouble", testStartTime, exportedItems, measurementValues);
        ValidateFirstPhase("exponentialHistogramDouble", testStartTime, exportedItems, measurementValues);
        ValidateFirstPhase("exponentialHistogramWithMinMaxLong", testStartTime, exportedItems, measurementValues);
        ValidateFirstPhase("exponentialHistogramLong", testStartTime, exportedItems, measurementValues);

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        var secondMeasurementValues = GenerateRandomValues(1, true, measurementValues);
        foreach (var value in secondMeasurementValues)
        {
            using var act = new Activity("test").Start();
            exponentialHistogramWithMinMaxDouble.Record(value.Value);
            exponentialHistogramDouble.Record(value.Value);
            exponentialHistogramWithMinMaxLong.Record((long)value.Value);
            exponentialHistogramLong.Record((long)value.Value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);

        ValidateSecondPhase("exponentialHistogramWithMinMaxDouble", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);
        ValidateSecondPhase("exponentialHistogramDouble", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);
        ValidateSecondPhase("exponentialHistogramWithMinMaxLong", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);
        ValidateSecondPhase("exponentialHistogramLong", temporality, testStartTime, exportedItems, measurementValues, secondMeasurementValues);

        static void ValidateFirstPhase(
            string instrumentName,
            DateTime testStartTime,
            List<Metric> exportedItems,
            (double Value, bool ExpectTraceId)[] measurementValues)
        {
            var metricPoint = GetFirstMetricPoint(exportedItems.Where(m => m.Name == instrumentName));

            Assert.NotNull(metricPoint);
            Assert.True(metricPoint.Value.StartTime >= testStartTime);
            Assert.True(metricPoint.Value.EndTime != default);

            var exemplars = GetExemplars(metricPoint.Value);

            ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, e => e.DoubleValue);
        }

        static void ValidateSecondPhase(
            string instrumentName,
            MetricReaderTemporalityPreference temporality,
            DateTime testStartTime,
            List<Metric> exportedItems,
            (double Value, bool ExpectTraceId)[] firstMeasurementValues,
            (double Value, bool ExpectTraceId)[] secondMeasurementValues)
        {
            var metricPoint = GetFirstMetricPoint(exportedItems.Where(m => m.Name == instrumentName));

            Assert.NotNull(metricPoint);
            Assert.True(metricPoint.Value.StartTime >= testStartTime);
            Assert.True(metricPoint.Value.EndTime != default);

            var exemplars = GetExemplars(metricPoint.Value);

            if (temporality == MetricReaderTemporalityPreference.Cumulative)
            {
                Assert.Equal(20, exemplars.Count);
                secondMeasurementValues = secondMeasurementValues.Concat(firstMeasurementValues.Skip(1).Take(19)).ToArray();
            }
            else
            {
                Assert.Single(exemplars);
            }

            ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, secondMeasurementValues, e => e.DoubleValue);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TestTraceBasedExemplarFilter(bool enableTracing)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());

        var counter = meter.CreateCounter<long>("testCounter");

        using var container = BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.TraceBased)
            .AddInMemoryExporter(exportedItems));

        if (enableTracing)
        {
            using var act = new Activity("test").Start();
            act.ActivityTraceFlags = ActivityTraceFlags.Recorded;
            counter.Add(18);
        }
        else
        {
            counter.Add(18);
        }

        meterProvider.ForceFlush();

        Assert.Single(exportedItems);

        var metricPoint = GetFirstMetricPoint(exportedItems);

        Assert.NotNull(metricPoint);

        var exemplars = GetExemplars(metricPoint.Value);

        if (enableTracing)
        {
            Assert.Single(exemplars);
        }
        else
        {
            Assert.Empty(exemplars);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TestExemplarsFilterTags(bool enableTagFiltering)
    {
        var exportedItems = new List<Metric>();

        using var meter = new Meter(Utils.GetCurrentMethodName());

        var histogram = meter.CreateHistogram<double>("testHistogram");

        TestExemplarReservoir? testExemplarReservoir = null;

        using var container = BuildMeterProvider(out var meterProvider, builder => builder
            .AddMeter(meter.Name)
            .SetExemplarFilter(ExemplarFilterType.AlwaysOn)
            .AddView(
                histogram.Name,
                new MetricStreamConfiguration()
                {
                    TagKeys = enableTagFiltering ? ["key1"] : null,
                    ExemplarReservoirFactory = () =>
                    {
                        if (testExemplarReservoir != null)
                        {
                            throw new InvalidOperationException();
                        }

                        return testExemplarReservoir = new TestExemplarReservoir();
                    },
                })
            .AddInMemoryExporter(exportedItems));

        histogram.Record(
            0,
            new("key1", "value1"),
            new("key2", "value2"),
            new("key3", "value3"));

        meterProvider.ForceFlush();

        Assert.NotNull(testExemplarReservoir);
        Assert.NotNull(testExemplarReservoir.MeasurementTags);
        Assert.Equal(3, testExemplarReservoir.MeasurementTags.Length);
        Assert.Contains(testExemplarReservoir.MeasurementTags, t => t.Key == "key1" && (string?)t.Value == "value1");
        Assert.Contains(testExemplarReservoir.MeasurementTags, t => t.Key == "key2" && (string?)t.Value == "value2");
        Assert.Contains(testExemplarReservoir.MeasurementTags, t => t.Key == "key3" && (string?)t.Value == "value3");

        var metricPoint = GetFirstMetricPoint(exportedItems);

        Assert.NotNull(metricPoint);

        var exemplars = GetExemplars(metricPoint.Value);

        Assert.NotNull(exemplars);

        foreach (var exemplar in exemplars)
        {
            if (!enableTagFiltering)
            {
                Assert.Equal(0, exemplar.FilteredTags.MaximumCount);
            }
            else
            {
                Assert.Equal(3, exemplar.FilteredTags.MaximumCount);

                var filteredTags = exemplar.FilteredTags.ToReadOnlyList();

                Assert.Equal(2, filteredTags.Count);

                Assert.Contains(new("key2", "value2"), filteredTags);
                Assert.Contains(new("key3", "value3"), filteredTags);
            }
        }
    }

    private static (double Value, bool ExpectTraceId)[] GenerateRandomValues(
        int count,
        bool expectTraceId,
        (double Value, bool ExpectTraceId)[]? previousValues)
    {
        var random = new Random();
        var values = new (double, bool)[count];
        for (int i = 0; i < count; i++)
        {
            var nextValue = random.NextDouble() * 100_000;
            if (values.Any(m => m.Item1 == nextValue || m.Item1 == (long)nextValue)
                || previousValues?.Any(m => m.Value == nextValue || m.Value == (long)nextValue) == true)
            {
                i--;
                continue;
            }

            values[i] = (nextValue, expectTraceId);
        }

        return values;
    }

    private static void ValidateExemplars(
        IReadOnlyList<Exemplar> exemplars,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        IEnumerable<(double Value, bool ExpectTraceId)> measurementValues,
        Func<Exemplar, double> getExemplarValueFunc)
    {
        int count = 0;

        var measurements = measurementValues.ToArray();

        foreach (var exemplar in exemplars)
        {
            Assert.True(exemplar.Timestamp >= startTime && exemplar.Timestamp <= endTime, $"{startTime} < {exemplar.Timestamp} < {endTime}");
            Assert.Equal(0, exemplar.FilteredTags.MaximumCount);

            var measurement = measurements.FirstOrDefault(v => v.Value == getExemplarValueFunc(exemplar)
                                                               || (long)v.Value == getExemplarValueFunc(exemplar));
            Assert.NotEqual(default, measurement);
            if (measurement.ExpectTraceId)
            {
                Assert.NotEqual(default, exemplar.TraceId);
                Assert.NotEqual(default, exemplar.SpanId);
            }
            else
            {
                Assert.Equal(default, exemplar.TraceId);
                Assert.Equal(default, exemplar.SpanId);
            }

            count++;
        }

        Assert.Equal(measurements.Length, count);
    }

    private sealed class TestExemplarReservoir : FixedSizeExemplarReservoir
    {
        public TestExemplarReservoir()
            : base(1)
        {
        }

        public KeyValuePair<string, object?>[]? MeasurementTags { get; private set; }

        public override void Offer(in ExemplarMeasurement<double> measurement)
        {
            this.MeasurementTags = measurement.Tags.ToArray();

            this.UpdateExemplar(0, in measurement);
        }

        public override void Offer(in ExemplarMeasurement<long> measurement)
        {
            throw new NotSupportedException();
        }
    }
}
