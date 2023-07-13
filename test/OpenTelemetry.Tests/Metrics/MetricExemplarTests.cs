// <copyright file="MetricExemplarTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Tests;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests;

public class MetricExemplarTests : MetricTestsBase
{
    private const int MaxTimeToAllowForFlush = 10000;
    private readonly ITestOutputHelper output;

    public MetricExemplarTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public void TestExemplarsCounter()
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var counter = meter.CreateCounter<double>("testCounter");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(new AlwaysOnExemplarFilter())
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
            })
            .Build();

        var measurementValues = GenerateRandomValues(10);
        foreach (var value in measurementValues)
        {
            counter.Add(value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        var exemplars = GetExemplars(metricPoint.Value);
        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, false);

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        measurementValues = GenerateRandomValues(10);
        foreach (var value in measurementValues)
        {
            var act = new Activity("test").Start();
            counter.Add(value);
            act.Stop();
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        exemplars = GetExemplars(metricPoint.Value);
        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, true);
    }

    [Fact]
    public void TestExemplarsHistogram()
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var histogram = meter.CreateHistogram<double>("testHistogram");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(new AlwaysOnExemplarFilter())
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
            })
            .Build();

        var measurementValues = GenerateRandomValues(10);
        foreach (var value in measurementValues)
        {
            histogram.Record(value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        var exemplars = GetExemplars(metricPoint.Value);
        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, false);

        exportedItems.Clear();

#if NETFRAMEWORK
        Thread.Sleep(10); // Compensates for low resolution timing in netfx.
#endif

        measurementValues = GenerateRandomValues(10);
        foreach (var value in measurementValues)
        {
            using var act = new Activity("test").Start();
            histogram.Record(value);
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        exemplars = GetExemplars(metricPoint.Value);
        ValidateExemplars(exemplars, metricPoint.Value.StartTime, metricPoint.Value.EndTime, measurementValues, true);
    }

    [Fact]
    public void TestExemplarsFilterTags()
    {
        DateTime testStartTime = DateTime.UtcNow;
        var exportedItems = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}");
        var histogram = meter.CreateHistogram<double>("testHistogram");
        using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .SetExemplarFilter(new AlwaysOnExemplarFilter())
            .AddView(histogram.Name, new MetricStreamConfiguration() { TagKeys = new string[] { "key1" } })
            .AddInMemoryExporter(exportedItems, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
            })
            .Build();

        var measurementValues = GenerateRandomValues(10);
        foreach (var value in measurementValues)
        {
            histogram.Record(value, new("key1", "value1"), new("key2", "value1"), new("key3", "value1"));
        }

        meterProvider.ForceFlush(MaxTimeToAllowForFlush);
        var metricPoint = GetFirstMetricPoint(exportedItems);
        Assert.NotNull(metricPoint);
        Assert.True(metricPoint.Value.StartTime >= testStartTime);
        Assert.True(metricPoint.Value.EndTime != default);
        var exemplars = GetExemplars(metricPoint.Value);
        Assert.NotNull(exemplars);
        foreach (var exemplar in exemplars)
        {
            Assert.NotNull(exemplar.FilteredTags);
            Assert.Contains(new("key2", "value1"), exemplar.FilteredTags);
            Assert.Contains(new("key3", "value1"), exemplar.FilteredTags);
        }
    }

    private static double[] GenerateRandomValues(int count)
    {
        var random = new Random();
        var values = new double[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = random.NextDouble();
        }

        return values;
    }

    private static void ValidateExemplars(Exemplar[] exemplars, DateTimeOffset startTime, DateTimeOffset endTime, double[] measurementValues, bool traceContextExists)
    {
        Assert.NotNull(exemplars);
        foreach (var exemplar in exemplars)
        {
            Assert.True(exemplar.Timestamp >= startTime && exemplar.Timestamp <= endTime, $"{startTime} < {exemplar.Timestamp} < {endTime}");
            Assert.Contains(exemplar.DoubleValue, measurementValues);
            Assert.Null(exemplar.FilteredTags);
            if (traceContextExists)
            {
                Assert.NotEqual(default, exemplar.TraceId);
                Assert.NotEqual(default, exemplar.SpanId);
            }
            else
            {
                Assert.Equal(default, exemplar.TraceId);
                Assert.Equal(default, exemplar.SpanId);
            }
        }
    }
}
