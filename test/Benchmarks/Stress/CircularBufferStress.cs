// <copyright file="CircularBufferStress.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Benchmarks
{
    internal class CircularBufferStress
    {
        private static long cntEvents = 0;

        public static int EntryPoint()
        {
            var cntWriter = Environment.ProcessorCount; // configure the number of writers
            var buffer = new CircularBuffer<Item>(500000); // configure the circular buffer capacity, change to a smaller number to test the congestion
            long bound = 100000000L; // each writer will write [1, bound]
            var statistics = new long[cntWriter];
            var retry = new long[cntWriter];
            long result = bound * (bound + 1) * cntWriter / 2;

            Console.WriteLine($"Inserting [0, {bound}] from {cntWriter} writers to a buffer with capacity={buffer.Capacity}.");

            Parallel.Invoke(
            () =>
            {
                var watch = new Stopwatch();

                while (result != 0)
                {
                    cntEvents = statistics.Sum();
                    watch.Restart();
                    Thread.Sleep(200);
                    watch.Stop();
                    var nEvents = statistics.Sum();
                    var nEventPerSecond = (int)((double)(nEvents - cntEvents) / ((double)watch.ElapsedMilliseconds / 1000.0));
                    var cntRetry = retry.Sum();
                    Console.Title = string.Format($"QueueSize: {buffer.Count}/{buffer.Capacity}, Retry: {cntRetry}, Enqueue: {nEvents}, Enqueue/s: {nEventPerSecond}, Result: {result}");
                }
            }, () =>
            {
                Parallel.For(0, statistics.Length, (i) =>
                {
                    statistics[i] = 0;
                    long num = 1;

                    Console.WriteLine($"Writer {i} started.");

                    while (true)
                    {
                        var item = new Item(num);

                        while (!buffer.TryAdd(item, 0))
                        {
                            retry[i]++;
                        }

                        num += 1;
                        statistics[i]++;

                        if (num > bound)
                        {
                            break;
                        }
                    }

                    Console.WriteLine($"Writer {i} finished.");
                });
            }, () =>
            {
                Console.WriteLine($"Reader started.");

                while (true)
                {
                    foreach (var item in new Batch<Item>(buffer, 100))
                    {
                        result -= item.Value;
                    }

                    if (result == 0)
                    {
                        break;
                    }
                }

                Console.WriteLine($"Reader finished.");
            });

            Console.WriteLine("Succedded!");
            return 0;
        }

        internal class Item
        {
            internal Item(long value)
            {
                this.Value = value;
            }

            public long Value { get; private set; }
        }
    }
}
