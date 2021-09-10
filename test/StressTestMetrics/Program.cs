// <copyright file="Program.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Metrics.Tests.Stress
{
    public class Program
    {
        private static readonly Meter MyMeter = new Meter("TestMeter", "0.0.1");
        private static readonly Counter<long> Counter = MyMeter.CreateCounter<long>("counter");
        private static string[] dimensionValues = new string[] { "DimVal1", "DimVal2", "DimVal3", "DimVal4", "DimVal5", "DimVal6", "DimVal7", "DimVal8", "DimVal9", "DimVal10" };
        private static Random random = new Random();

        public static void Main(string[] args)
        {
            long numberOfMetricWriters = Environment.ProcessorCount;
            long maxWritesPerWriter = 10000000;
            var writes = new long[numberOfMetricWriters];
            using var meterProvider = Sdk.CreateMeterProviderBuilder()
                .AddSource("TestMeter")
                .Build();
            Stopwatch sw = new Stopwatch();
            sw.Start();

            Parallel.Invoke(
                () =>
                {
                    var watch = new Stopwatch();
                    while (true)
                    {
                        long totalWrites = 0;
                        for (int i = 0; i < numberOfMetricWriters; i++)
                        {
                            totalWrites += writes[i];
                        }

                        watch.Restart();
                        Thread.Sleep(1000);
                        watch.Stop();

                        long newTotalWrites = 0;
                        for (int i = 0; i < numberOfMetricWriters; i++)
                        {
                            newTotalWrites += writes[i];
                        }

                        var writesPerSec = (newTotalWrites - totalWrites) / (watch.ElapsedMilliseconds / 1000.0);
                        Console.Title = $"Writes (Million/Sec): {writesPerSec / 1000000}";

                        if (totalWrites > numberOfMetricWriters * maxWritesPerWriter)
                        {
                            break;
                        }
                    }
                }, () =>
                {
                    Parallel.For(0, numberOfMetricWriters, i
                    =>
                    {
                        Console.WriteLine($"Metric writer {i} started.");
                        while (writes[i]++ < maxWritesPerWriter)
                        {
                            // 10 * 10 * 10 = 1000 unique combination is produced
                            // which is well within the current hard-coded cap of
                            // 2000.
                            var tag1 = new KeyValuePair<string, object>("DimName1", dimensionValues[random.Next(0, 10)]);
                            var tag2 = new KeyValuePair<string, object>("DimName2", dimensionValues[random.Next(0, 10)]);
                            var tag3 = new KeyValuePair<string, object>("DimName3", dimensionValues[random.Next(0, 10)]);
                            Counter.Add(100, tag1, tag2, tag3);
                        }

                        Console.WriteLine($"Metric writer {i} completed.");
                    });
                });

            var rate = (double)(numberOfMetricWriters * maxWritesPerWriter) / (sw.ElapsedMilliseconds / 1000);
            Console.WriteLine($"{rate / 1000000} M/sec.");
        }
    }
}
