// <copyright file="OpenTelemetryMetricsBuilderExtensionsTests.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Diagnostics.Metrics;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Tests;
using OpenTelemetry.Tests;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Extensions.Hosting.Tests;

public class OpenTelemetryMetricsBuilderExtensionsTests
{
    [Fact]
    public void EnableMetricsTest()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = new();

        using (var host = MetricTestsBase.BuildHost(
            configureMetricsBuilder: builder => builder.EnableMetrics(meter.Name),
            configureMeterProviderBuilder: builder => builder.AddInMemoryExporter(exportedItems)))
        {
            var counter = meter.CreateCounter<long>("TestCounter");
            counter.Add(1);
        }

        Assert.Single(exportedItems);

        List<MetricPoint> metricPoints = new();
        foreach (ref readonly var mp in exportedItems[0].GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);

        var metricPoint = metricPoints[0];
        Assert.Equal(1, metricPoint.GetSumLong());
    }

    [Fact]
    public void EnableMetricsWithAddMeterTest()
    {
        using var meter = new Meter(Utils.GetCurrentMethodName());
        List<Metric> exportedItems = new();

        using (var host = MetricTestsBase.BuildHost(
            configureMetricsBuilder: builder => builder.EnableMetrics(meter.Name),
            configureMeterProviderBuilder: builder => builder
                .AddSdkMeter(meter.Name)
                .AddInMemoryExporter(exportedItems)))
        {
            var counter = meter.CreateCounter<long>("TestCounter");
            counter.Add(1);
        }

        Assert.Single(exportedItems);

        List<MetricPoint> metricPoints = new();
        foreach (ref readonly var mp in exportedItems[0].GetMetricPoints())
        {
            metricPoints.Add(mp);
        }

        Assert.Single(metricPoints);

        var metricPoint = metricPoints[0];
        Assert.Equal(1, metricPoint.GetSumLong());
    }
}
