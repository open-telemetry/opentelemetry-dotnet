// <copyright file="MetricViewTests.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;
using System.Diagnostics.Metrics;
using OpenTelemetry.Exporter;
using Xunit;
using Xunit.Abstractions;

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricViewTests
    {
        private readonly ITestOutputHelper output;

        public MetricViewTests(ITestOutputHelper output)
        {
            this.output = output;
        }

        [Fact]
        public void ViewToRenameMetric()
        {
            var exportedItems = new List<Metric>();
            var inMemoryReader = new BaseExportingMetricReader(new InMemoryExporter<Metric>(exportedItems));
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
            .AddSource("TestMeter1")
            .AddReader(inMemoryReader)
            .AddView("name1", "renamed")
            .Build();

            using var meter1 = new Meter("TestMeter1");

            // Expecting one metric stream.
            var counterLong = meter1.CreateCounter<long>("name1");
            counterLong.Add(10);
            inMemoryReader.Collect();
            Assert.Single(exportedItems);
            var metric = exportedItems[0];
            Assert.Equal("renamed", metric.Name);
        }
    }
}
