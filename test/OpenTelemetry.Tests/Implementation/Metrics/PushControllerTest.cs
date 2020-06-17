// <copyright file="PushControllerTest.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenTelemetry.Metrics.Export;
using Xunit;
using static OpenTelemetry.Metrics.Configuration.MeterFactory;

namespace OpenTelemetry.Metrics.Test
{
    public class PushControllerTest
    {
        [Fact]
        public void PushControllerCollectsAllMeters()
        {
            // Setup controller to collect every 25 msec.
            var controllerPushIntervalInMsec = 25;
            var collectionCountExpectedMin = 3;
            var maxWaitInMsec = (controllerPushIntervalInMsec * collectionCountExpectedMin) + 2000;

            int exportCalledCount = 0;
            var testExporter = new TestMetricExporter(() => exportCalledCount++);

            var testProcessor = new TestMetricProcessor();

            // Setup 2 meters whose Collect will increment the collect count.
            int meter1CollectCount = 0;
            int meter2CollectCount = 0;
            var meters = new Dictionary<MeterRegistryKey, MeterSdk>();
            var testMeter1 = new TestMeter("meter1", testProcessor, () => meter1CollectCount++);
            meters.Add(new MeterRegistryKey("meter1", string.Empty), testMeter1);
            var testMeter2 = new TestMeter("meter2", testProcessor, () => meter2CollectCount++);
            meters.Add(new MeterRegistryKey("meter2", string.Empty), testMeter2);

            var pushInterval = TimeSpan.FromMilliseconds(controllerPushIntervalInMsec);
            var pushController = new PushMetricController(
                meters,
                testProcessor,
                testExporter,
                pushInterval,
                new CancellationTokenSource());

            // Validate that collect is called on Meter1, Meter2.
            this.ValidateMeterCollect(ref meter1CollectCount, collectionCountExpectedMin, "meter1", TimeSpan.FromMilliseconds(maxWaitInMsec));
            this.ValidateMeterCollect(ref meter2CollectCount, collectionCountExpectedMin, "meter2", TimeSpan.FromMilliseconds(maxWaitInMsec));

            // Export must be called same no: of times as Collect.
            Assert.True(exportCalledCount >= collectionCountExpectedMin);
        }

        private void ValidateMeterCollect(ref int meterCollectCount, int expectedMeterCollectCount, string meterName, TimeSpan timeout)
        {
            // Sleep in short intervals, so the actual test duration is not always the max wait time.
            var sw = Stopwatch.StartNew();
            while (meterCollectCount < expectedMeterCollectCount && sw.Elapsed <= timeout)
            {
                Thread.Sleep(10);
            }

            Assert.True(
                meterCollectCount >= expectedMeterCollectCount
                && meterCollectCount <= expectedMeterCollectCount,
                $"Actual Collect Count for meter: {meterName} is {meterCollectCount} vs Expected count of {expectedMeterCollectCount}");
        }
    }
}
