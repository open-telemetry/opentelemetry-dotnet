// <copyright file="CircularBufferBenchmarks.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Benchmarks.Internal
{
    public class CircularBufferBenchmarks
    {
        [Benchmark]
        public void TryAddBenchmark()
        {
            var writerCount = 4; // use fixed writer count instead of core count based to prevent very long iterations while core count was software limited
            var buffer = new CircularBuffer<Item>(500); // configure the circular buffer capacity, change to a smaller number to test the congestion
            var bound = 100000L; // each writer will write [1, bound]
            var result = bound * (bound + 1) * writerCount / 2;
            Parallel.Invoke(
                () =>
                {
                }, () =>
                {
                    Parallel.For(0, writerCount, (i) =>
                    {
                        long num = 1;
                        while (true)
                        {
                            var item = new Item(num);

                            while (!buffer.TryAdd(item, 10000))
                            {
                                // manual yield is required while testing without change introduced in #1424
                                // (if it isn't used we can stuck in the loop)
                                Thread.Yield();
                            }

                            num += 1;

                            if (num > bound)
                            {
                                break;
                            }
                        }
                    });
                }, () =>
                {
                    while (true)
                    {
                        if (buffer.Count > 0)
                        {
                            result -= buffer.Read().Value;
                        }

                        if (result == 0)
                        {
                            break;
                        }
                    }
                });
            if (result != 0)
            {
                throw new InvalidOperationException("Final result should equals to zero");
            }
        }

        internal class Item
        {
            internal Item(long value)
            {
                this.Value = value;
            }

            public long Value { get; }
        }
    }
}
