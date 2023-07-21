// <copyright file="MultipleReadersTests.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests;

public class MultipleReadersTests
{
    [Theory]
    [InlineData(MetricReaderTemporalityPreference.Delta, false)]
    [InlineData(MetricReaderTemporalityPreference.Delta, true)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, false)]
    [InlineData(MetricReaderTemporalityPreference.Cumulative, true)]
    public void SdkSupportsMultipleReaders(MetricReaderTemporalityPreference aggregationTemporality, bool hasViews)
    {
        var exportedItems1 = new List<Metric>();
        var exportedItems2 = new List<Metric>();

        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{aggregationTemporality}.{hasViews}");

        var counter = meter.CreateCounter<long>("counter");

        int index = 0;
        var values = new long[] { 100, 200, 300, 400 };
        long GetValue() => values[index++];
        var gauge = meter.CreateObservableGauge("gauge", () => GetValue());

        int indexSum = 0;
        var valuesSum = new long[] { 1000, 1200, 1300, 1400 };
        long GetSum() => valuesSum[indexSum++];
        var observableCounter = meter.CreateObservableCounter("obs-counter", () => GetSum());

        bool defaultNamedOptionsConfigureCalled = false;
        bool namedOptionsConfigureCalled = false;

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .ConfigureServices(services =>
            {
                services.Configure<MetricReaderOptions>(o =>
                {
                    defaultNamedOptionsConfigureCalled = true;
                });
                services.Configure<MetricReaderOptions>("Exporter2", o =>
                {
                    namedOptionsConfigureCalled = true;
                });
            })
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems1, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = MetricReaderTemporalityPreference.Delta;
            })
            .AddInMemoryExporter("Exporter2", exportedItems2, metricReaderOptions =>
            {
                metricReaderOptions.TemporalityPreference = aggregationTemporality;
            });

        if (hasViews)
        {
            meterProviderBuilder.AddView("counter", "renamedCounter");
            meterProviderBuilder.AddView("gauge", "renamedGauge");
            meterProviderBuilder.AddView("obs-counter", "renamedObservableCounter");
        }

        using var meterProvider = meterProviderBuilder.Build();

        Assert.True(defaultNamedOptionsConfigureCalled);
        Assert.True(namedOptionsConfigureCalled);

        counter.Add(10, new KeyValuePair<string, object>("key", "value"));

        meterProvider.ForceFlush();

        Assert.Equal(3, exportedItems1.Count);
        Assert.Equal(3, exportedItems2.Count);

        if (hasViews)
        {
            Assert.Equal("renamedCounter", exportedItems1[0].Name);
            Assert.Equal("renamedCounter", exportedItems2[0].Name);

            Assert.Equal("renamedGauge", exportedItems1[1].Name);
            Assert.Equal("renamedGauge", exportedItems2[1].Name);

            Assert.Equal("renamedObservableCounter", exportedItems1[2].Name);
            Assert.Equal("renamedObservableCounter", exportedItems2[2].Name);
        }
        else
        {
            Assert.Equal("counter", exportedItems1[0].Name);
            Assert.Equal("counter", exportedItems2[0].Name);

            Assert.Equal("gauge", exportedItems1[1].Name);
            Assert.Equal("gauge", exportedItems2[1].Name);

            Assert.Equal("obs-counter", exportedItems1[2].Name);
            Assert.Equal("obs-counter", exportedItems2[2].Name);
        }

        // Check value exported for Counter
        AssertLongSumValueForMetric(exportedItems1[0], 10);
        AssertLongSumValueForMetric(exportedItems2[0], 10);

        // Check value exported for Gauge
        AssertLongSumValueForMetric(exportedItems1[1], 100);
        AssertLongSumValueForMetric(exportedItems2[1], 200);

        // Check value exported for ObservableCounter
        AssertLongSumValueForMetric(exportedItems1[2], 1000);
        if (aggregationTemporality == MetricReaderTemporalityPreference.Delta)
        {
            AssertLongSumValueForMetric(exportedItems2[2], 1200);
        }
        else
        {
            AssertLongSumValueForMetric(exportedItems2[2], 1200);
        }

        exportedItems1.Clear();
        exportedItems2.Clear();

        counter.Add(15, new KeyValuePair<string, object>("key", "value"));

        meterProvider.ForceFlush();

        Assert.Equal(3, exportedItems1.Count);
        Assert.Equal(3, exportedItems2.Count);

        // Check value exported for Counter
        AssertLongSumValueForMetric(exportedItems1[0], 15);
        if (aggregationTemporality == MetricReaderTemporalityPreference.Delta)
        {
            AssertLongSumValueForMetric(exportedItems2[0], 15);
        }
        else
        {
            AssertLongSumValueForMetric(exportedItems2[0], 25);
        }

        // Check value exported for Gauge
        AssertLongSumValueForMetric(exportedItems1[1], 300);
        AssertLongSumValueForMetric(exportedItems2[1], 400);

        // Check value exported for ObservableCounter
        AssertLongSumValueForMetric(exportedItems1[2], 300);
        if (aggregationTemporality == MetricReaderTemporalityPreference.Delta)
        {
            AssertLongSumValueForMetric(exportedItems2[2], 200);
        }
        else
        {
            AssertLongSumValueForMetric(exportedItems2[2], 1400);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ObservableInstrumentCallbacksInvokedForEachReaders(bool hasViews)
    {
        var exportedItems1 = new List<Metric>();
        var exportedItems2 = new List<Metric>();
        using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{hasViews}");
        int callbackInvocationCount = 0;
        var gauge = meter.CreateObservableGauge("gauge", () =>
        {
            callbackInvocationCount++;
            return 10 * callbackInvocationCount;
        });

        var meterProviderBuilder = Sdk.CreateMeterProviderBuilder()
            .AddMeter(meter.Name)
            .AddInMemoryExporter(exportedItems1)
            .AddInMemoryExporter(exportedItems2);

        if (hasViews)
        {
            meterProviderBuilder.AddView("gauge", "renamedGauge");
        }

        using var meterProvider = meterProviderBuilder.Build();
        meterProvider.ForceFlush();

        // VALIDATE
        Assert.Equal(2, callbackInvocationCount);
        Assert.Single(exportedItems1);
        Assert.Single(exportedItems2);

        if (hasViews)
        {
            Assert.Equal("renamedGauge", exportedItems1[0].Name);
            Assert.Equal("renamedGauge", exportedItems2[0].Name);
        }
        else
        {
            Assert.Equal("gauge", exportedItems1[0].Name);
            Assert.Equal("gauge", exportedItems2[0].Name);
        }
    }

    private static void AssertLongSumValueForMetric(Metric metric, long value)
    {
        var metricPoints = metric.GetMetricPoints();
        var metricPointsEnumerator = metricPoints.GetEnumerator();
        Assert.True(metricPointsEnumerator.MoveNext()); // One MetricPoint is emitted for the Metric
        ref readonly var metricPointForFirstExport = ref metricPointsEnumerator.Current;
        if (metric.MetricType.IsSum())
        {
            Assert.Equal(value, metricPointForFirstExport.GetSumLong());
        }
        else
        {
            Assert.Equal(value, metricPointForFirstExport.GetGaugeLastValueLong());
        }
    }
}
