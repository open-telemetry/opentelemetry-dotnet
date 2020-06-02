// <copyright file="CounterCleanUpTests.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using OpenTelemetry.Metrics;
using OpenTelemetry.Metrics.Configuration;
using OpenTelemetry.Metrics.Export;
using OpenTelemetry.Metrics.Test;
using OpenTelemetry.Trace;
using Xunit;

namespace OpenTelemetry.Configuration.Tests
{
    public class MeterFactoryTests
    {
        [Fact]
        public void DefaultMeterShouldBeCollectedAsWell()
        {
            var testProcessor = new TestMetricProcessor();
            var factory = MeterFactory.Create(mb => mb.SetMetricProcessor(testProcessor));
            var controller = factory.PushMetricController;
            var defaultMeter = factory.GetMeter(string.Empty) as MeterSdk;

            // Record some metrics using default meter
            var testCounter = defaultMeter.CreateInt64Counter("testCounter");
            var context = default(SpanContext);
            var labels = LabelSet.BlankLabelSet;
            testCounter.Add(context, 100, labels);
            testCounter.Add(context, 10, labels);

            // Collect using PushMetricController
            var sw = Stopwatch.StartNew();
            var metricToExport = controller.Collect(sw).ToList();

            Assert.Single(metricToExport);
            Assert.Single(metricToExport[0].Data);
            Assert.Equal(110, (metricToExport[0].Data[0] as Int64SumData).Sum);
        }
    }
}
