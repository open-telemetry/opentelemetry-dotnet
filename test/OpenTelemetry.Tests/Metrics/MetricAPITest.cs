// <copyright file="MetricAPITest.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using Xunit;

#nullable enable

namespace OpenTelemetry.Metrics.Tests
{
    public class MetricApiTest
    {
        [Fact]
        public void SimpleTest()
        {
        }

        /*
        [Fact]
        public void ViewTest()
        {
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddSource("BasicAllTest")
                .SetDefaultCollectionPeriod(1000)
                .AddProcessor(new TagEnrichmentProcessor("newAttrib", "newAttribValue"))
                .AddView(
                    meterName: "BasicAllTest",
                    aggregator: Aggregator.SUMMARY,
                    attributeKeys: new string[] { "label1", "label2" },
                    viewName: "test")
                .AddExportProcessor(new MetricConsoleExporter("Test1"))
                .Build();

            using var meter = new Meter("BasicAllTest", "0.0.1");

            var counter = meter.CreateCounter<int>("counter");

            counter.Add(
                100,
                new KeyValuePair<string, object?>("label1", "value1"),
                new KeyValuePair<string, object?>("label2", "value2"));

            counter.Add(
                100,
                new KeyValuePair<string, object?>("label1", "value1"),
                new KeyValuePair<string, object?>("label3", "value3"));

            Task.Delay(3000).Wait();
        }
        */
    }
}
