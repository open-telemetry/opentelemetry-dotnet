// <copyright file="TestMetrics.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Metrics;

namespace Examples.Console
{
    internal class TestMetrics
    {
        internal static object Run(int observationInterval)
        {
            using var provider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter") // All instruments from this meter are enabled.
                .SetObservationPeriod(observationInterval)
                .Build();

            using var meter = new Meter("TestMeter", "0.0.1");

            var counter = meter.CreateCounter<int>("counter1");
            counter.Add(10);

            counter.Add(
                100,
                new KeyValuePair<string, object>("label1", "value1"));

            counter.Add(
                200,
                new KeyValuePair<string, object>("label1", "value1"),
                new KeyValuePair<string, object>("label2", "value2"));

            var observableCounter = meter.CreateObservableGauge<int>("CurrentMemoryUsage", () =>
            {
                return new List<Measurement<int>>()
                {
                    new Measurement<int>(
                        (int)Process.GetCurrentProcess().PrivateMemorySize64,
                        new KeyValuePair<string, object>("attrb1", "value1")),
                };
            });

            Task.Delay(50).Wait();
            System.Console.WriteLine("Press Enter key to exit.");
            return null;
        }
    }
}
