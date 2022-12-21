// <copyright file="MemoryEfficiencyTests.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Tests;
using Xunit;

namespace OpenTelemetry.Metrics.Tests
{
    public class MemoryEfficiencyTests
    {
        [Theory]
        [InlineData(MetricReaderTemporalityPreference.Cumulative)]
        [InlineData(MetricReaderTemporalityPreference.Delta)]
        public void ExportOnlyWhenPointChanged(MetricReaderTemporalityPreference temporality)
        {
            using var meter = new Meter($"{Utils.GetCurrentMethodName()}.{temporality}");

            var exportedItems = new List<Metric>();

            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddMeter(meter.Name)
                .AddInMemoryExporter(exportedItems, metricReaderOptions =>
                {
                    metricReaderOptions.TemporalityPreference = temporality;
                })
                .Build();

            var counter = meter.CreateCounter<long>("meter");

            counter.Add(10, new KeyValuePair<string, object>("tag1", "value1"));
            meterProvider.ForceFlush();
            Assert.Single(exportedItems);

            exportedItems.Clear();
            meterProvider.ForceFlush();
            if (temporality == MetricReaderTemporalityPreference.Cumulative)
            {
                Assert.Single(exportedItems);
            }
            else
            {
                Assert.Empty(exportedItems);
            }
        }
    }
}
