// <copyright file="TestBenchmarks.cs" company="OpenTelemetry Authors">
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

using System.Threading;
using BenchmarkDotNet.Attributes;

namespace Benchmarks.Metrics
{
    [MemoryDiagnoser]
    public class TestBenchmarks
    {
        private int isLocked = 1;

        [Benchmark]
        public void Exchange()
        {
            var temp = Interlocked.Exchange(ref this.isLocked, 1) == 0;
        }

        [Benchmark]
        public void CompareExchange()
        {
            var temp = Interlocked.CompareExchange(ref this.isLocked, 1, 0) == 0;
        }
    }
}
